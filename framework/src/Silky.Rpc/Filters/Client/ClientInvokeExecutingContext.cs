﻿using System.Collections.Generic;
using Silky.Core.FilterMetadata;
using Silky.Rpc.Runtime.Client;
using Silky.Rpc.Transport.Messages;

namespace Silky.Rpc.Filters;

public class ClientInvokeExecutingContext : ClientFilterContext
{
    public ClientInvokeExecutingContext(ClientInvokeContext context,IList<IFilterMetadata> filters) : base(context, filters)
    {
    }

    public virtual RemoteResultMessage? Result { get; set; }
}