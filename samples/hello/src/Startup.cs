using System;
using Owin;

namespace Hello
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.UseHandler(async (req, res) =>
                {
                    res.StatusCode = 200;
                    res.ContentType = "text/plain";
                    await res.WriteAsync("Hello, from C#. Time on server is " + DateTime.Now);
                });
        }
    }
}
