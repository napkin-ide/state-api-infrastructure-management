using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using LCU.StateAPI.Utilities;
using LCU.Personas.Client.Enterprises;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using LCU.Presentation.State.ReqRes;

namespace LCU.State.API.NapkinIDE.Infrastructure.Management
{
    [Serializable]
    public class InfrastructureManagementState
    {
        #region Fields
        protected readonly EnterpriseArchitectClient entArch;

        protected readonly EnterpriseManagerClient entMgr;

        #endregion

        #region Constants
        public const string HUB_NAME = "infrastructuremanagement";
        #endregion

        #region Properties 
        public virtual string CouldBeAnything { get; set; }

        public virtual StateDetails StateDetails { get; set; }
        #endregion

        #region Constructors
        public InfrastructureManagementState(StateDetails stateDetails)
        {
            this.StateDetails = stateDetails;
        }
        #endregion

        #region API Methods
        public virtual void SetCouldBeAnything(string anything)
        {
            CouldBeAnything = anything;
        }
        #endregion
    }

    public static class InfrastructureManagementStateEntity
    {
        [FunctionName("InfrastructureManagementState")]
        public static void Run([EntityTrigger] IDurableEntityContext ctx, ILogger log)
        {
            var action = ctx.OperationName.ToLowerInvariant();
            

            var state = ctx.GetState<InfrastructureManagementState>();

            if (action == "$init" && state == null)
                state = new InfrastructureManagementState(ctx.GetInput<StateDetails>());

            switch (action)
            {
                case "SetCouldBeAnything":
                    var actionReq = ctx.GetInput<ExecuteActionRequest>();

                    state.SetCouldBeAnything(actionReq.Arguments.Metadata["Anything"].ToString());
                    break;
            }

            ctx.SetState(state);

            ctx.StartNewOrchestration("SendState", state);

            // ctx.Return(state);
        }
    }
}
