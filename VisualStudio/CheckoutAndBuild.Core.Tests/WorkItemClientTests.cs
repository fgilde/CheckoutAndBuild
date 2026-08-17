using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CheckoutAndBuild.Core.WorkItems;
using Xunit;

namespace CheckoutAndBuild.Core.Tests
{
	public class WorkItemClientTests
	{
		private sealed class FakeHandler : HttpMessageHandler
		{
			private readonly Func<HttpRequestMessage, string, HttpResponseMessage> responder;

			public List<(HttpRequestMessage Request, string Body)> Calls { get; } = new List<(HttpRequestMessage, string)>();

			public FakeHandler(Func<HttpRequestMessage, string, HttpResponseMessage> responder) => this.responder = responder;

			public static HttpResponseMessage Json(string json) => new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(json, Encoding.UTF8, "application/json")
			};

			protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
			{
				string body = request.Content == null ? null : await request.Content.ReadAsStringAsync();
				Calls.Add((request, body));
				return responder(request, body);
			}
		}

		private static WorkItemClient CreateClient(FakeHandler handler) =>
			new WorkItemClient("https://dev.azure.com/myorg", "MyProject", "secret-pat", handler);

		[Fact]
		public async Task QueryIdsAsync_ParsesFlatResult_AndSendsPatAuth()
		{
			var handler = new FakeHandler((req, body) => FakeHandler.Json("{\"queryType\":\"flat\",\"workItems\":[{\"id\":1},{\"id\":42}]}"));
			using (var client = CreateClient(handler))
			{
				var ids = await client.QueryIdsAsync("SELECT [System.Id] FROM WorkItems");

				Assert.Equal(new[] { 1, 42 }, ids);
				var (request, body) = handler.Calls.Single();
				Assert.Equal("https://dev.azure.com/myorg/MyProject/_apis/wit/wiql?api-version=7.1", request.RequestUri.ToString());
				Assert.Equal("Basic", request.Headers.Authorization.Scheme);
				Assert.Equal(Convert.ToBase64String(Encoding.ASCII.GetBytes(":secret-pat")), request.Headers.Authorization.Parameter);
				Assert.Contains("SELECT [System.Id]", body);
			}
		}

		[Fact]
		public async Task QueryIdsAsync_ParsesTreeResult_Distinct()
		{
			var handler = new FakeHandler((req, body) => FakeHandler.Json(
				"{\"queryType\":\"tree\",\"workItemRelations\":[{\"target\":{\"id\":5}},{\"source\":{\"id\":5},\"target\":{\"id\":7}},{\"target\":{\"id\":5}}]}"));
			using (var client = CreateClient(handler))
			{
				var ids = await client.QueryIdsAsync("tree query");
				Assert.Equal(new[] { 5, 7 }, ids);
			}
		}

		[Fact]
		public async Task GetTextFieldsAsync_FiltersToTextTypes()
		{
			var handler = new FakeHandler((req, body) => FakeHandler.Json(@"{""value"":[
				{""referenceName"":""System.Title"",""name"":""Title"",""type"":""string""},
				{""referenceName"":""System.Description"",""name"":""Description"",""type"":""html""},
				{""referenceName"":""Custom.Steps"",""name"":""Steps"",""type"":""plainText""},
				{""referenceName"":""System.Id"",""name"":""ID"",""type"":""integer""}]}"));
			using (var client = CreateClient(handler))
			{
				var fields = await client.GetTextFieldsAsync();

				Assert.Equal(3, fields.Count);
				Assert.Equal("Title", fields["System.Title"]);
				Assert.False(fields.ContainsKey("System.Id"));
			}
		}

		[Fact]
		public async Task GetWorkItemsAsync_BatchesRequests_AndKeepsStringFields()
		{
			var handler = new FakeHandler((req, body) =>
			{
				using (var doc = JsonDocument.Parse(body))
				{
					var items = doc.RootElement.GetProperty("ids").EnumerateArray()
						.Select(id => $"{{\"id\":{id.GetInt32()},\"fields\":{{\"System.Title\":\"T{id.GetInt32()}\",\"Microsoft.VSTS.Common.Priority\":2}}}}");
					return FakeHandler.Json($"{{\"value\":[{string.Join(",", items)}]}}");
				}
			});
			using (var client = CreateClient(handler))
			{
				var ids = Enumerable.Range(1, 250).ToArray();
				var workItems = await client.GetWorkItemsAsync(ids);

				Assert.Equal(2, handler.Calls.Count);
				Assert.Equal(250, workItems.Count);
				Assert.Equal("T1", workItems[0].Title);
				Assert.False(workItems[0].Fields.ContainsKey("Microsoft.VSTS.Common.Priority")); // non-string skipped
			}
		}

		[Fact]
		public async Task UpdateFieldsAsync_SendsJsonPatch()
		{
			var handler = new FakeHandler((req, body) => FakeHandler.Json("{\"id\":7}"));
			using (var client = CreateClient(handler))
			{
				await client.UpdateFieldsAsync(7, new Dictionary<string, string> { ["System.Title"] = "new title" });

				var (request, body) = handler.Calls.Single();
				Assert.Equal("PATCH", request.Method.Method);
				Assert.Equal("https://dev.azure.com/myorg/_apis/wit/workitems/7?api-version=7.1", request.RequestUri?.ToString());
				Assert.Equal("application/json-patch+json", request.Content?.Headers.ContentType?.MediaType);
				Assert.Contains("\"op\":\"add\"", body);
				Assert.Contains("/fields/System.Title", body);
				Assert.Contains("new title", body);
			}
		}

		[Fact]
		public async Task FailedRequest_ThrowsWithServerMessage()
		{
			var handler = new FakeHandler((req, body) => new HttpResponseMessage(HttpStatusCode.Unauthorized)
			{
				Content = new StringContent("{\"message\":\"PAT expired\"}", Encoding.UTF8, "application/json")
			});
			using (var client = CreateClient(handler))
			{
				var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.QueryIdsAsync("q"));
				Assert.Contains("401", ex.Message);
				Assert.Contains("PAT expired", ex.Message);
			}
		}

		[Fact]
		public void FindMatches_MatchesOnlyTextFieldsContainingTerm()
		{
			var workItems = new[]
			{
				new WorkItemData { Id = 1, Fields = { ["System.Title"] = "fix foo bug", ["System.State"] = "foo" } },
				new WorkItemData { Id = 2, Fields = { ["System.Title"] = "unrelated", ["System.Description"] = "foo inside" } },
				new WorkItemData { Id = 3, Fields = { ["System.Title"] = "nothing here" } }
			};
			var textFields = new HashSet<string> { "System.Title", "System.Description" };

			var matches = WorkItemSearch.FindMatches(workItems, textFields, "foo");

			Assert.Equal(2, matches.Count);
			Assert.Equal(new[] { "System.Title" }, matches[1]); // System.State not in text field set
			Assert.Equal(new[] { "System.Description" }, matches[2]);
			Assert.Empty(WorkItemSearch.FindMatches(workItems, textFields, ""));
		}
	}
}
