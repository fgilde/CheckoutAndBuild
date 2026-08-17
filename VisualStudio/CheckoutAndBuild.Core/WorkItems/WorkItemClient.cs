using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CheckoutAndBuild.Core.WorkItems
{
	/// <summary>A work item with its string-valued fields (reference name → value).</summary>
	public class WorkItemData
	{
		public int Id { get; set; }
		public Dictionary<string, string> Fields { get; } = new Dictionary<string, string>();

		public string Title => Fields.TryGetValue("System.Title", out string title) ? title : "";
		public string WorkItemType => Fields.TryGetValue("System.WorkItemType", out string type) ? type : "";
		public string State => Fields.TryGetValue("System.State", out string state) ? state : "";
		public string AssignedTo => Fields.TryGetValue("System.AssignedTo", out string assigned) ? assigned : "";
	}

	/// <summary>
	/// Azure-DevOps work item access over the plain REST API (PAT auth) —
	/// no TeamFoundation client libraries, reusable outside the IDE.
	/// </summary>
	public class WorkItemClient : IDisposable
	{
		private const string apiVersion = "7.1";
		private const int batchSize = 200;

		private readonly HttpClient http;
		private readonly string organizationUrl;
		private readonly string project;

		/// <param name="organizationUrl">e.g. https://dev.azure.com/myorg or https://server/tfs/DefaultCollection</param>
		public WorkItemClient(string organizationUrl, string project, string personalAccessToken, HttpMessageHandler handler = null)
		{
			if (string.IsNullOrWhiteSpace(organizationUrl))
				throw new ArgumentNullException(nameof(organizationUrl));
			this.organizationUrl = organizationUrl.TrimEnd('/');
			this.project = project;
			http = handler == null ? new HttpClient() : new HttpClient(handler);
			http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
				Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + (personalAccessToken ?? ""))));
		}

		/// <summary>Web URL for opening a work item in the browser.</summary>
		public string GetWorkItemUrl(int id) => $"{organizationUrl}/{Uri.EscapeDataString(project ?? "")}/_workitems/edit/{id}";

		/// <summary>Runs a WIQL query and returns the matching work item ids (flat and tree queries).</summary>
		public async Task<IReadOnlyList<int>> QueryIdsAsync(string wiql, CancellationToken ct = default)
		{
			string url = $"{organizationUrl}/{Uri.EscapeDataString(project ?? "")}/_apis/wit/wiql?api-version={apiVersion}";
			using (JsonDocument doc = await PostAsync(url, JsonSerializer.Serialize(new { query = wiql }), ct).ConfigureAwait(false))
			{
				var ids = new List<int>();
				if (doc.RootElement.TryGetProperty("workItems", out JsonElement flat))
				{
					foreach (JsonElement item in flat.EnumerateArray())
						ids.Add(item.GetProperty("id").GetInt32());
				}
				else if (doc.RootElement.TryGetProperty("workItemRelations", out JsonElement relations))
				{
					foreach (JsonElement relation in relations.EnumerateArray())
					{
						if (relation.TryGetProperty("target", out JsonElement target))
							ids.Add(target.GetProperty("id").GetInt32());
					}
				}
				return ids.Distinct().ToList();
			}
		}

		/// <summary>All searchable text fields (string/plainText/html) as reference name → display name.</summary>
		public async Task<IReadOnlyDictionary<string, string>> GetTextFieldsAsync(CancellationToken ct = default)
		{
			string url = $"{organizationUrl}/_apis/wit/fields?api-version={apiVersion}";
			using (JsonDocument doc = await GetAsync(url, ct).ConfigureAwait(false))
			{
				var fields = new Dictionary<string, string>();
				foreach (JsonElement field in doc.RootElement.GetProperty("value").EnumerateArray())
				{
					string type = field.GetProperty("type").GetString();
					if (type == "string" || type == "plainText" || type == "html")
						fields[field.GetProperty("referenceName").GetString()] = field.GetProperty("name").GetString();
				}
				return fields;
			}
		}

		/// <summary>Loads work items with all string-valued fields, batching requests as the API requires.</summary>
		public async Task<IReadOnlyList<WorkItemData>> GetWorkItemsAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
		{
			var result = new List<WorkItemData>();
			for (int offset = 0; offset < ids.Count; offset += batchSize)
			{
				var chunk = ids.Skip(offset).Take(batchSize).ToArray();
				string url = $"{organizationUrl}/_apis/wit/workitemsbatch?api-version={apiVersion}";
				string body = JsonSerializer.Serialize(new Dictionary<string, object> { ["ids"] = chunk, ["$expand"] = "fields" });
				using (JsonDocument doc = await PostAsync(url, body, ct).ConfigureAwait(false))
				{
					foreach (JsonElement item in doc.RootElement.GetProperty("value").EnumerateArray())
					{
						var data = new WorkItemData { Id = item.GetProperty("id").GetInt32() };
						foreach (JsonProperty field in item.GetProperty("fields").EnumerateObject())
						{
							if (field.Value.ValueKind == JsonValueKind.String)
								data.Fields[field.Name] = field.Value.GetString();
							else if (field.Value.ValueKind == JsonValueKind.Object
								&& field.Value.TryGetProperty("displayName", out JsonElement displayName))
								data.Fields[field.Name] = displayName.GetString();
						}
						result.Add(data);
					}
				}
			}
			return result;
		}

		/// <summary>Updates the given fields of one work item via JSON patch.</summary>
		public async Task UpdateFieldsAsync(int id, IEnumerable<KeyValuePair<string, string>> fieldValues, CancellationToken ct = default)
		{
			string url = $"{organizationUrl}/_apis/wit/workitems/{id}?api-version={apiVersion}";
			var patch = fieldValues.Select(pair => new { op = "add", path = "/fields/" + pair.Key, value = pair.Value }).ToArray();
			var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
			{
				Content = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json-patch+json")
			};
			using (HttpResponseMessage response = await http.SendAsync(request, ct).ConfigureAwait(false))
				await EnsureSuccessAsync(response).ConfigureAwait(false);
		}

		private async Task<JsonDocument> GetAsync(string url, CancellationToken ct)
		{
			using (HttpResponseMessage response = await http.GetAsync(url, ct).ConfigureAwait(false))
				return await ReadAsync(response).ConfigureAwait(false);
		}

		private async Task<JsonDocument> PostAsync(string url, string body, CancellationToken ct)
		{
			using (HttpResponseMessage response = await http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"), ct).ConfigureAwait(false))
				return await ReadAsync(response).ConfigureAwait(false);
		}

		private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response)
		{
			await EnsureSuccessAsync(response).ConfigureAwait(false);
			string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
			return JsonDocument.Parse(json);
		}

		private static async Task EnsureSuccessAsync(HttpResponseMessage response)
		{
			if (response.IsSuccessStatusCode)
				return;
			string body = response.Content == null ? "" : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
			string message = TryGetErrorMessage(body) ?? response.ReasonPhrase;
			throw new InvalidOperationException($"Azure DevOps request failed ({(int)response.StatusCode}): {message}");
		}

		private static string TryGetErrorMessage(string body)
		{
			try
			{
				using (JsonDocument doc = JsonDocument.Parse(body))
					return doc.RootElement.TryGetProperty("message", out JsonElement message) ? message.GetString() : null;
			}
			catch (JsonException)
			{
				return null;
			}
		}

		public void Dispose() => http.Dispose();
	}

	/// <summary>Pure match logic for the search&amp;replace preview (kept IDE-free for tests and the Rider port).</summary>
	public static class WorkItemSearch
	{
		/// <summary>Per work item id: the text field reference names whose value contains <paramref name="searchTerm"/>.</summary>
		public static Dictionary<int, List<string>> FindMatches(IEnumerable<WorkItemData> workItems, ICollection<string> textFieldRefNames, string searchTerm)
		{
			var matches = new Dictionary<int, List<string>>();
			if (string.IsNullOrEmpty(searchTerm))
				return matches;
			foreach (WorkItemData workItem in workItems)
			{
				foreach (var field in workItem.Fields)
				{
					if (!textFieldRefNames.Contains(field.Key) || field.Value == null || !field.Value.Contains(searchTerm))
						continue;
					if (!matches.TryGetValue(workItem.Id, out List<string> fields))
						matches[workItem.Id] = fields = new List<string>();
					fields.Add(field.Key);
				}
			}
			return matches;
		}
	}
}
