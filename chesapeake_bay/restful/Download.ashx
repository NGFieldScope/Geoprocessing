<%@ WebHandler Language="C#" Class="Download" %>

using System;
using System.Web;

public class Download : IHttpHandler {

    public void ProcessRequest (HttpContext context) {
        context.Response.ContentType = context.Request.Params["ContentType"];
        context.Response.AppendHeader("Content-Disposition",
                                      String.Format("attachment; filename={0}",
                                                    context.Request.Params["FileName"]));
        context.Response.BinaryWrite(Convert.FromBase64String(context.Request.Params["File"]));
    }

    public bool IsReusable {
        get {
            return false;
        }
    }
}
