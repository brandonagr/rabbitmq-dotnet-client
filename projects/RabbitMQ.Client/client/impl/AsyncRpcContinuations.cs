// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2020 VMware, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       https://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v2.0:
//
//---------------------------------------------------------------------------
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using RabbitMQ.Client.client.framing;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    internal abstract class AsyncRpcContinuation<T> : IRpcContinuation
    {
        protected readonly TaskCompletionSource<T> _tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskAwaiter<T> GetAwaiter() => _tcs.Task.GetAwaiter();

        public abstract void HandleCommand(in IncomingCommand cmd);

        public void HandleChannelShutdown(ShutdownEventArgs reason) => _tcs.SetException(new OperationInterruptedException(reason));
    }

    internal class ConnectionSecureOrTuneContinuation : AsyncRpcContinuation<ConnectionSecureOrTune>
    {
        public override void HandleCommand(in IncomingCommand cmd)
        {
            try
            {
                if (cmd.CommandId == ProtocolCommandId.ConnectionSecure)
                {
                    var secure = new ConnectionSecure(cmd.MethodBytes.Span);
                    _tcs.TrySetResult(new ConnectionSecureOrTune { m_challenge = secure._challenge });
                }
                else if (cmd.CommandId == ProtocolCommandId.ConnectionTune)
                {
                    var tune = new ConnectionTune(cmd.MethodBytes.Span);
                    _tcs.TrySetResult(new ConnectionSecureOrTune
                    {
                        m_tuneDetails = new() { m_channelMax = tune._channelMax, m_frameMax = tune._frameMax, m_heartbeatInSeconds = tune._heartbeat }
                    });
                }
                else
                {
                    _tcs.SetException(new InvalidOperationException($"Received unexpected command of type {cmd.CommandId}!"));
                }
            }
            finally
            {
                cmd.ReturnMethodBuffer();
            }
        }
    }

    internal class SimpleAsyncRpcContinuation : AsyncRpcContinuation<bool>
    {
        private readonly ProtocolCommandId _expectedCommandId;

        public SimpleAsyncRpcContinuation(ProtocolCommandId expectedCommandId)
        {
            _expectedCommandId = expectedCommandId;
        }

        public override void HandleCommand(in IncomingCommand cmd)
        {
            try
            {
                if (cmd.CommandId == _expectedCommandId)
                {
                    _tcs.TrySetResult(true);
                }
                else
                {
                    _tcs.SetException(new InvalidOperationException($"Received unexpected command of type {cmd.CommandId}!"));
                }
            }
            finally
            {
                cmd.ReturnMethodBuffer();
            }
        }
    }

    internal class ExchangeDeclareAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public ExchangeDeclareAsyncRpcContinuation() : base(ProtocolCommandId.ExchangeDeclareOk)
        {
        }
    }

    internal class ExchangeDeleteAsyncRpcContinuation : SimpleAsyncRpcContinuation
    {
        public ExchangeDeleteAsyncRpcContinuation() : base(ProtocolCommandId.ExchangeDeleteOk)
        {
        }
    }

    internal class QueueDeclareAsyncRpcContinuation : AsyncRpcContinuation<QueueDeclareOk>
    {
        public override void HandleCommand(in IncomingCommand cmd)
        {
            try
            {
                var method = new Client.Framing.Impl.QueueDeclareOk(cmd.MethodBytes.Span);
                var result = new QueueDeclareOk(method._queue, method._messageCount, method._consumerCount);
                if (cmd.CommandId == ProtocolCommandId.QueueDeclareOk)
                {
                    _tcs.TrySetResult(result);
                }
                else
                {
                    _tcs.SetException(new InvalidOperationException($"Received unexpected command of type {cmd.CommandId}!"));
                }
            }
            finally
            {
                cmd.ReturnMethodBuffer();
            }
        }
    }

    internal class QueueDeleteAsyncRpcContinuation : AsyncRpcContinuation<QueueDeleteOk>
    {
        public override void HandleCommand(in IncomingCommand cmd)
        {
            try
            {
                var result = new Client.Framing.Impl.QueueDeleteOk(cmd.MethodBytes.Span);
                if (cmd.CommandId == ProtocolCommandId.QueueDeleteOk)
                {
                    _tcs.TrySetResult(result);
                }
                else
                {
                    _tcs.SetException(new InvalidOperationException($"Received unexpected command of type {cmd.CommandId}!"));
                }
            }
            finally
            {
                cmd.ReturnMethodBuffer();
            }
        }
    }
}
