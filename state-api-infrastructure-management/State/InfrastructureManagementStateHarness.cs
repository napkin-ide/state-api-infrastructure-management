using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Fathym;
using LCU.Presentation.State.ReqRes;
using LCU.StateAPI.Utilities;
using LCU.StateAPI;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;
using System.Collections.Generic;
using LCU.Personas.Client.Enterprises;
using LCU.Personas.Client.DevOps;
using LCU.Personas.Enterprises;
using LCU.Personas.Client.Applications;
using LCU.Personas.Client.Identity;
using Fathym.API;
using LCU.Personas.Client.Security;

namespace LCU.State.API.NapkinIDE.InfrastructureManagement.State
{
    public class InfrastructureManagementStateHarness : LCUStateHarness<InfrastructureManagementState>
    {
        #region Constants
        public const string FATHYM_DASHBOARD_URL_LOOKUP = "LCU.Fathym-Dashboard-URL";
        #endregion

        #region Fields 
        #endregion

        #region Properties 
        #endregion

        #region Constructors
        public InfrastructureManagementStateHarness(InfrastructureManagementState state)
            : base(state ?? new InfrastructureManagementState())
        { }
        #endregion

        #region API Methods
        public virtual async Task GetFathymDashboardURL(SecurityManagerClient secMgr, string entLookup)
        {
            var tpdResp = await secMgr.RetrieveEnterpriseThirdPartyData(entLookup, FATHYM_DASHBOARD_URL_LOOKUP);

            if (tpdResp.Status)
                State.FathymDashboardURL = tpdResp.Model[FATHYM_DASHBOARD_URL_LOOKUP];
        }

        public virtual async Task SetFathymDashboardURL(SecurityManagerClient secMgr, string entLookup, string url)
        {
            State.FathymDashboardURL = url;

            await secMgr.SetEnterpriseThirdPartyData(entLookup, new Dictionary<string, string>()
            {
                { FATHYM_DASHBOARD_URL_LOOKUP, url }
            });
        }
        #endregion
    }
}
