using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Owin.Samples
{
    public class Startup
    {
    	public Task Invoke(IDictionary<string, object> env)
    	{
            env["owin.ResponseStatusCode"] = 200;
            ((IDictionary<string, string[]>)env["owin.ResponseHeaders"]).Add(
                "Content-Type", new string[] { "text/html" });
            StreamWriter w = new StreamWriter((Stream)env["owin.ResponseBody"]);
            w.Write("Hello, from C#. Time on server is " + DateTime.Now.ToString());
            w.Flush();

            return Task.FromResult<object>(null);
    	}
    }
}
