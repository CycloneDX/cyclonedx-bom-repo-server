// This file is part of CycloneDX BOM Repository Server
//
// Licensed under the Apache License, Version 2.0 (the “License”);
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an “AS IS” BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) OWASP Foundation. All Rights Reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CycloneDX.BomRepoServer.Services
{
    public class RetentionBackgroundService : IHostedService
    {
        private Task _executingTask;
        private readonly CancellationTokenSource _stoppingCts = new CancellationTokenSource();
        private readonly ILogger<RetentionBackgroundService> _logger;
        private readonly RetentionService _retentionService;

        public RetentionBackgroundService(ILogger<RetentionBackgroundService> logger, RetentionService retentionService)
        {
            _logger = logger;
            _retentionService = retentionService;
        }
 
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _executingTask = ExecuteAsync(_stoppingCts.Token);
            return _executingTask.IsCompleted ? _executingTask : Task.CompletedTask;
        }
 
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_executingTask == null) return;
 
            try
            {
                _stoppingCts.Cancel();
            }
            finally
            {
                await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
            }
        }
 
        private async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            do
            {
                _logger.LogInformation("Updating BOM cache...");
                await _retentionService.ProcessRetention();
 
                await Task.Delay(TimeSpan.FromMinutes(60), stoppingToken);
            }
            while (!stoppingToken.IsCancellationRequested);
        }
    }
}