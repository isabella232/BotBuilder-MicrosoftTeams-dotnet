﻿// <copyright file="TestHelpers.cs" company="Microsoft">
// Licensed under the MIT License.
// </copyright>

namespace Microsoft.Bot.Builder.Teams.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Bot.Builder.Adapters;
    using Microsoft.Bot.Builder.Teams.Middlewares;
    using Microsoft.Bot.Connector;
    using Microsoft.Bot.Connector.Authentication;
    using Microsoft.Bot.Schema;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Common test helpers.
    /// </summary>
    internal class TestHelpers
    {
        /// <summary>
        /// Runs the test pipeline with activity asynchronously.
        /// </summary>
        /// <param name="activity">The activity.</param>
        /// <param name="callback">The callback.</param>
        /// <returns>Task tracking operation.</returns>
        internal static async Task RunTestPipelineWithActivityAsync(Activity activity, Func<ITeamsContext, Task> callback)
        {
            Mock<ICredentialProvider> mockCredentialProvider = new Mock<ICredentialProvider>();
            TestAdapter testAdapter = new TestAdapter(new ConversationReference(activity.Id, activity.From, activity.Recipient, activity.Conversation, activity.ChannelId, activity.ServiceUrl));
            testAdapter.Use(new TeamsMiddleware(mockCredentialProvider.Object));
            await testAdapter.ProcessActivityAsync(
                activity,
                async (turnContext, cancellationToken) =>
                {
                    ITeamsContext teamsContext = turnContext.TurnState.Get<ITeamsContext>();
                    await callback(teamsContext).ConfigureAwait(false);
                }).ConfigureAwait(false);
        }

        /// <summary>
        /// Tests card attachment before and after sending match.
        /// </summary>
        /// <param name="attachment">Attachment to verify.</param>
        /// <returns>Task tracking operation.</returns>
        internal static async Task TestAttachmentAsync(Attachment attachment)
        {
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings();
            serializerSettings.NullValueHandling = NullValueHandling.Ignore;
            Activity activity = JsonConvert.DeserializeObject<Activity>(File.ReadAllText(@"Jsons\SampleSkeletonActivity.json"));
            activity.Attachments = new List<Attachment>() { attachment };

            TestDelegatingHandler testDelegatingHandler = new TestDelegatingHandler((request) =>
            {
                string data = (request.Content as StringContent).ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                Activity receivedActivity = JsonConvert.DeserializeObject<Activity>(data, serializerSettings);

                Assert.AreEqual(receivedActivity.Attachments.Count, activity.Attachments.Count);
                Assert.IsTrue(JObject.DeepEquals(
                    JObject.FromObject(activity.Attachments[0].Content, JsonSerializer.Create(serializerSettings)),
                    JObject.FromObject(receivedActivity.Attachments[0].Content)));

                ResourceResponse resourceResponse = new ResourceResponse("TestId");
                StringContent responseContent = new StringContent(JsonConvert.SerializeObject(resourceResponse));
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = responseContent;
                return Task.FromResult(response);
            });

            ConnectorClient conClient = new ConnectorClient(
                new Uri("https://testservice.com"),
                new MicrosoftAppCredentials("Test", "Test"),
                testDelegatingHandler);

            ResourceResponse callResponse = await conClient.Conversations.SendToConversationAsync(activity).ConfigureAwait(false);

            Assert.IsTrue(conClient.Conversations.SendToConversation(activity).Id == "TestId");
        }
    }
}
