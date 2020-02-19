using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Fathym;
using LCU.StateAPI.Utilities;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace LCU.State.API.NapkinIDE.Infrastructure.Management
{
    public static class SendState
    {
        [FunctionName("SendState")]
        public static async Task<Status> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var state = context.GetInput<InfrastructureManagementState>();

            return await context.CallActivityAsync<Status>("EmitState", state);
        }

        [FunctionName("EmitState")]
        public static async Task<Status> EmitState([ActivityTrigger] InfrastructureManagementState state, ILogger log,
            [SignalR(HubName = InfrastructureManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages)
        {
            var stateDetails = state.StateDetails;

            var groupName = StateUtils.BuildGroupName(stateDetails);

            var sendMethod = $"ReceiveState";

            await signalRMessages.AddAsync(new SignalRMessage()
            {
                Target = sendMethod,
                GroupName = "Test",//groupName,
                Arguments = new[] { state }
            });

            return Status.Success;
        }
    }
}