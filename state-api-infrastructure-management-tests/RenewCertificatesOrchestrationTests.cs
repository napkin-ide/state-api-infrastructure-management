using Fathym.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace state_api_infrastructure_management_tests
{
    [TestClass]
    public class RenewCertificatesOrchestrationTests : AzFunctionTestBase
    {
        
        public RenewCertificatesOrchestrationTests() : base()
        {
            APIRoute = "api/RenewCertificatesOrchestration";                
        }

        [TestMethod]
        public async Task TestRenewCertificatesOrchestration()
        {
            LcuentLookup = "";            
            PrincipalId = "";

            addRequestHeaders();

            var url = $"{HostURL}/{APIRoute}";            

            var response = await httpGet(url); 

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            var model = getContent<dynamic>(response);

            dynamic result = model.Result;            

            throw new NotImplementedException("Implement me!");                  
        }


        [TestMethod]
        public async Task TestRetrieveHostsForRenewal()
        {
            LcuentLookup = "";            
            PrincipalId = "";

            addRequestHeaders();

            var url = $"{HostURL}/RetrieveHostsForRenewal";            

            var response = await httpGet(url); 

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            var model = getContent<dynamic>(response);

            dynamic result = model.Result;            

            throw new NotImplementedException("Implement me!");                  
        }      

        [TestMethod]
        public async Task TestRetrieveRenewalEnvironments()
        {
            LcuentLookup = "";            
            PrincipalId = "";

            addRequestHeaders();

            var url = $"{HostURL}/RetrieveRenewalEnvironments";            

            var response = await httpGet(url); 

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            var model = getContent<dynamic>(response);

            dynamic result = model.Result;            

            throw new NotImplementedException("Implement me!");                  
        }      

        [TestMethod]
        public async Task TestEnsureSSLCertificate()
        {
            LcuentLookup = "";            
            PrincipalId = "";

            addRequestHeaders();

            var url = $"{HostURL}/EnsureSSLCertificate";            

            var response = await httpGet(url); 

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            var model = getContent<dynamic>(response);

            dynamic result = model.Result;            

            throw new NotImplementedException("Implement me!");                  
        }             
    }
}
