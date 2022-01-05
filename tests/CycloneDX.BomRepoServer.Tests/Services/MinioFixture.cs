using System;
using System.IO;
using DotNet.Testcontainers.Containers.Builders;
using DotNet.Testcontainers.Containers.Modules;
using DotNet.Testcontainers.Containers.OutputConsumers;
using DotNet.Testcontainers.Containers.WaitStrategies;

public class MinioFixture : IDisposable {
    public MinioFixture() {
        var testcontainersBuilder = new TestcontainersBuilder<TestcontainersContainer>()
            .WithImage("minio/minio")
            .WithName("minio")
            .WithPortBinding(9000, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(9000))
            .WithCommand("server", "--console-address", ":9001", "/data")
            .WithCleanUp(true);

        TestContainer = testcontainersBuilder.Build();
        TestContainer.StartAsync().GetAwaiter().GetResult();
    }

    public TestcontainersContainer TestContainer { get; }

    public void Dispose()
    {
        TestContainer.StopAsync().GetAwaiter().GetResult();
    }
}