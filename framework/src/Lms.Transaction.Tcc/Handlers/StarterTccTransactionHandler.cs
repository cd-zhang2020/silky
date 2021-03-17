﻿using System;
using System.Threading.Tasks;
using Lms.Core.DynamicProxy;
using Lms.Transaction.Handler;
using Lms.Transaction.Tcc.Executor;

namespace Lms.Transaction.Tcc.Handlers
{
    public class StarterTccTransactionHandler : ITransactionHandler
    {
        private TccTransactionExecutor executor = TccTransactionExecutor.Executor;

        public async Task Handler(TransactionContext context, ILmsMethodInvocation invocation)
        {
            var transaction = executor.PreTry(invocation);
            try
            {
                await invocation.ProceedAsync();
                transaction.Status = ActionStage.Trying;
                executor.UpdateStartStatus(transaction);
                await executor.GlobalConfirm(transaction);
            }
            catch (Exception e)
            {
                await executor.GlobalCancel(transaction);
                throw;
            }
        }
    }
}