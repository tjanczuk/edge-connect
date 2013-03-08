using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Owin.Connect
{
    public class OwinConnectHttpServer
    {

        static List<Func<IDictionary<string, object>, Task>> owinHandlers = new List<Func<IDictionary<string, object>, Task>>();

        public Task<object> Configure(object input)
        {
            /*
             * This method performs initialization of the OWIN application given the configuration parameters 
             * in `input`. The return value is an integer or a string represeting a unique identifier of the application instance. 
             * That unique identifier will be later provided to the Invoke method to allow dispatching the actual HTTP request
             * processing to the appropriate OWIN application instance. 
             * 
             * The `input` parameter is an IDictionary<string,object> representing the 
             * application configuration parameters specified when calling the proxy to this function
             * from node.js. At minimum the dictionary will contain the `assemblyFile` property that contains
             * the file name of the assembly with the OWIN application. The dictionary may also contain any other 
             * properties the node.js developer has chosen to pass to Configure. It may be useful to pass-though these options
             * to initialization code of the OWIN application. 
             * 
             * This method should implement whatever OWIN conventions we see fit for loading an OWIN application. In particular, 
             * it should recognize the common application OM patterns and normalize them to a single representation of
             * Func<IDictionary<string,object>, Task>
             * 
             * This method does not need to be thread safe as it is always called from the singleton V8 thread 
             * of the node.js application.
             *
             * Future: I envision this method will at some point support Roslyn. In that world the options the node.js developer 
             * passes in will contain a *.csx file instead of a *.dll. Possibly it may also specify a C# literal to compile to
             * enable writing simple C# code snippets within *.js files themselves.
             */

            // determine assembly name

            string assemblyFile = this.GetValueOrDefault<string>(input, "assemblyFile", null);

            // determine type name

            // default type name is the assembly name plus ".Startup"
            string defaultTypeName = assemblyFile.Substring(Math.Max(0, assemblyFile.LastIndexOf('\\')));
            if (defaultTypeName.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase))
            {
                defaultTypeName = defaultTypeName.Substring(0, defaultTypeName.Length - 4);
            }
            defaultTypeName += ".Startup";
            string typeName = this.GetValueOrDefault<string>(input, "typeName", defaultTypeName);

            // determine method name

            string methodName = this.GetValueOrDefault<string>(input, "methodName", "Invoke");

            // Load OWIN application

            Assembly assembly = Assembly.LoadFrom(assemblyFile);
            Type type = assembly.GetType(typeName, true, true);
            object instance = Activator.CreateInstance(type, false);
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);

            // Normalize to Func<IDictionary<string, object>, Task>

            Func<IDictionary<string, object>, Task> invoke = (env) =>
            {
                return (Task)method.Invoke(instance, new object[] { env });
            };

            // Register the method and return its integer identifier to node.js. 
            // The identifier is the index into the owinHandlers list.
            // Note: no sychronization required since we are running on singleton node.js thread. 

            owinHandlers.Add(invoke);

            return Task.FromResult((object)(owinHandlers.Count - 1));
        }

        public async Task<object> Invoke(object input)
        {
            /*
             * This method invokes the actual OWIN handler to process the HTTP request. 
             * 
             * The `input` parameter is an IDictionary<string,object> that contains the following keys:
             * - owin.RequestMethod
             * - owin.RequestPath
             * - owin.RequestPathBase
             * - owin.RequestProtocol
             * - owin.RequestQueryString
             * - owin.RequestScheme
             * - owin.RequestHeaders
             * - owin.RequestBody
             * - owin-connect.owinAppId
             * - node.*
             * 
             * Most of the owin.* properties are already in the format requred by the OWIN spec. These are exceptions that require
             * preprocessing before passing to the actual OWIN handler:
             * 
             * The `owin.RequestBody` is a byte[]. It must be wrapped in an instance of MemoryStream.
             * The `owin.ResponseBody` is missing. A new MemoryStream must be created to represent it. 
             * The `owin.ResponseHeaders` is missing. A new entry must be created to represent it. 
             * 
             * The owin-connect.owinAppId is the identifier returned from the Configure method that should be used to dispatch
             * the request to appropriate handler.
             * 
             * The node.* is an arbitrary set of properties specified by node.js developer either at the time of 'configure', or
             * per-request via connect middleware running before the owin express handler. Typically they will contain proxies to 
             * node.js functions exported to .NET in the form of Func<object,Task<object>>. 
             * 
             * This method must pre-process the OWIN environment, invoke the OWIN handler, post-process the resulting OWIN environment,
             * and return it as the object result of this function. 
             * 
             * Postprocessing of the OWIN environment after invoking the OWIN application must:
             * - remove all node.* entries from the dictionary. This is required because we cannot marshal Func<object,Task<object>> back to node.js at this time.
             * - remove owin.Request* properties
             * - convert owin.ResponseBody to a byte[] and store it back in owin.ResponseBody
             * 
             * Future: non-JSON content types and full streaming support (basically we will be able to marshal node.js Stream as a .NET Stream from node.js)
             */

            IDictionary<string, object> env = (IDictionary<string, object>)input;

            // extract owinAppId

            int owinAppId = this.GetValueOrDefault<int>(env, "owin-connect.owinAppId", -1);

            // create memory stream around request body

            byte[] body = this.GetValueOrDefault<byte[]>(env, "owin.RequestBody", new byte[0]);
            env["owin.RequestBody"] = new MemoryStream(body);

            // create response OWIN properties for the application to write to

            MemoryStream responseBody = new MemoryStream();
            env["owin.ResponseBody"] = responseBody;
            Dictionary<string, string[]> responseHeaders = new Dictionary<string, string[]>();
            env["owin.ResponseHeaders"] = responseHeaders; 

            // run the OWIN app

            return await owinHandlers[owinAppId](env).ContinueWith<object>((task) =>
            {
                if (task.IsFaulted)
                {
                    throw task.Exception;
                }

                if (task.IsCanceled)
                {
                    throw new InvalidOperationException("The OWIN application has cancelled processing of the request.");
                }

                // remove all non-response entries from env

                List<string> keys = new List<string>(env.Keys);
                foreach (string key in keys)
                {
                    if (!key.StartsWith("owin.Response"))
                    {
                        env.Remove(key);
                    }
                }

                // serialize response body to a byte[]

                env["owin.ResponseBody"] = responseBody.GetBuffer();

                // normalize response headers to the representiation required by express.js

                Dictionary<string, string> responseHeaders4js = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string[]> header in responseHeaders)
                {
                    if (header.Value.Length > 0)
                    {
                        // TODO: how do you set multiple response headers with the same name in node.js anyway?
                        responseHeaders4js[header.Key] = header.Value[0];
                    }
                }

                env["owin.ResponseHeaders"] = responseHeaders4js;

                // return the post-processed env back to node.js

                return env;
            });
        }

        T GetValueOrDefault<T>(object input, string parameter, T defaultValue)
        {
            IDictionary<string, object> parameters = (IDictionary<string, object>)input;
            object value;
            if (parameters.TryGetValue(parameter, out value))
            {
                return (T)value;
            }
            else
            {
                return defaultValue;
            }
        }
    }
}
