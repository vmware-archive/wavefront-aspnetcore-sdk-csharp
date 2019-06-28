using App.Metrics;

namespace Wavefront.AspNetCore.SDK.CSharp.Common
{
    /// <summary>
    ///     ASP.NET Core SDK constants.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        ///     Name of the ASP.NET Core component.
        /// </summary>
        public static readonly string AspNetCoreComponent = "AspNetCore";

        /// <summary>
        ///     Name of the App Metrics context. 
        /// </summary>
        public static readonly string AspNetCoreContext = "AspNetCore";

        /// <summary>
        ///     Tag key for defining the ASP.NET Core MVC controller.
        /// </summary>
        public static readonly string ControllerTagKey = AspNetCoreContext + ".resource.controller";

        /// <summary>
        ///     Tag key for defining the ASP.NET Core MVC controller action.
        /// </summary>
        public static readonly string ActionTagKey = AspNetCoreContext + ".resource.action";

        /// <summary>
        ///     Tag key for defining the request path.
        /// </summary>
        public static readonly string PathTagKey = AspNetCoreContext + ".path";

        /// <summary>
        ///     App Metrics measurement unit tag value for responses.
        /// </summary>
        public static readonly Unit ResponseUnit = Unit.Custom("resp");

        /// <summary>
        ///     App Metrics measurement unit tag value for milliseconds.
        /// </summary>
        public static readonly Unit MillisecondUnit = Unit.Custom("ms");

        /// <summary>
        ///     Internal key for storing and accessing the unhandled exception of a particular request.
        /// </summary>
        public static readonly string ExceptionKey =
            "Wavefront.AspNetCore.SDK.CSharp.Mvc.Internal.Exception";
    }
}
