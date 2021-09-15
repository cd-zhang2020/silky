﻿using Silky.Core.Modularity;
using Silky.WebSocket;

namespace Microsoft.Extensions.Hosting
{
    [DependsOn(typeof(WebSocketModule)
    )]
    public class WebSocketHostModule : GeneralHostModule
    {
    }
}