package org.gilde.coab

import com.google.gson.Gson
import com.google.gson.JsonObject
import com.google.gson.JsonParser
import com.intellij.credentialStore.CredentialAttributes
import com.intellij.credentialStore.Credentials
import com.intellij.ide.passwordSafe.PasswordSafe
import java.net.URI
import java.net.URLEncoder
import java.net.http.HttpClient
import java.net.http.HttpRequest
import java.net.http.HttpResponse
import java.util.Base64

/** One work item row (string and identity fields only, like the Visual Studio version). */
data class WorkItem(val id: Int, val fields: Map<String, String>) {
    val title get() = fields["System.Title"] ?: ""
    val type get() = fields["System.WorkItemType"] ?: ""
    val state get() = fields["System.State"] ?: ""
    val assignedTo get() = fields["System.AssignedTo"] ?: ""
}

/** Azure DevOps work item access over the plain REST API with PAT auth (PAT stored in the IDE password safe). */
object AzdoClient {

    private const val apiVersion = "7.1"
    private val gson = Gson()
    private val http = HttpClient.newHttpClient()
    private val credentialKey = CredentialAttributes("CheckoutAndBuild.AzureDevOps")

    var pat: String
        get() = PasswordSafe.instance.getPassword(credentialKey) ?: ""
        set(value) {
            PasswordSafe.instance.set(credentialKey, if (value.isEmpty()) null else Credentials("pat", value))
        }

    fun itemUrl(organization: String, project: String, id: Int) =
        "${organization.trimEnd('/')}/${encode(project)}/_workitems/edit/$id"

    fun createUrl(organization: String, project: String, type: String) =
        "${organization.trimEnd('/')}/${encode(project)}/_workitems/create/${encode(type)}"

    fun queryIds(organization: String, project: String, wiql: String): List<Int> {
        val body = gson.toJson(mapOf("query" to wiql))
        val json = send("POST", "${organization.trimEnd('/')}/${encode(project)}/_apis/wit/wiql?api-version=$apiVersion", body)
        val ids = mutableListOf<Int>()
        json.getAsJsonArray("workItems")?.forEach { ids.add(it.asJsonObject.get("id").asInt) }
        if (ids.isEmpty()) json.getAsJsonArray("workItemRelations")?.forEach {
            it.asJsonObject.getAsJsonObject("target")?.get("id")?.asInt?.let(ids::add)
        }
        return ids.distinct()
    }

    fun textFields(organization: String): Map<String, String> {
        val json = send("GET", "${organization.trimEnd('/')}/_apis/wit/fields?api-version=$apiVersion", null)
        val result = mutableMapOf<String, String>()
        json.getAsJsonArray("value")?.forEach {
            val field = it.asJsonObject
            when (field.get("type")?.asString) {
                "string", "plainText", "html" ->
                    result[field.get("referenceName").asString] = field.get("name").asString
                else -> {}
            }
        }
        return result
    }

    fun workItems(organization: String, ids: List<Int>): List<WorkItem> {
        val result = mutableListOf<WorkItem>()
        for (chunk in ids.chunked(200)) {
            val body = gson.toJson(mapOf("ids" to chunk, "\$expand" to "fields"))
            val json = send("POST", "${organization.trimEnd('/')}/_apis/wit/workitemsbatch?api-version=$apiVersion", body)
            json.getAsJsonArray("value")?.forEach { element ->
                val item = element.asJsonObject
                val fields = mutableMapOf<String, String>()
                item.getAsJsonObject("fields")?.entrySet()?.forEach { (name, value) ->
                    when {
                        value.isJsonPrimitive && value.asJsonPrimitive.isString -> fields[name] = value.asString
                        value.isJsonObject && value.asJsonObject.has("displayName") ->
                            fields[name] = value.asJsonObject.get("displayName").asString
                        else -> {}
                    }
                }
                result.add(WorkItem(item.get("id").asInt, fields))
            }
        }
        return result
    }

    fun updateFields(organization: String, id: Int, values: Map<String, String>) {
        val patch = values.map { (field, value) -> mapOf("op" to "add", "path" to "/fields/$field", "value" to value) }
        val request = HttpRequest.newBuilder(URI.create(
            "${organization.trimEnd('/')}/_apis/wit/workitems/$id?api-version=$apiVersion"))
            .header("Authorization", authHeader())
            .header("Content-Type", "application/json-patch+json")
            .method("PATCH", HttpRequest.BodyPublishers.ofString(gson.toJson(patch)))
            .build()
        val response = http.send(request, HttpResponse.BodyHandlers.ofString())
        if (response.statusCode() >= 300) throw RuntimeException("Update of #$id failed (${response.statusCode()})")
    }

    private fun send(method: String, url: String, body: String?): JsonObject {
        val builder = HttpRequest.newBuilder(URI.create(url)).header("Authorization", authHeader())
        if (body != null) builder.header("Content-Type", "application/json")
            .method(method, HttpRequest.BodyPublishers.ofString(body))
        else builder.method(method, HttpRequest.BodyPublishers.noBody())
        val response = http.send(builder.build(), HttpResponse.BodyHandlers.ofString())
        if (response.statusCode() >= 300)
            throw RuntimeException("Azure DevOps request failed (${response.statusCode()}): ${response.body().take(300)}")
        return JsonParser.parseString(response.body()).asJsonObject
    }

    private fun authHeader() = "Basic " + Base64.getEncoder().encodeToString(":${pat}".toByteArray())

    private fun encode(value: String): String = URLEncoder.encode(value, Charsets.UTF_8).replace("+", "%20")
}
