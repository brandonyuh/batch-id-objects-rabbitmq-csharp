using System.Collections.Concurrent;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class RpcClient : IDisposable
{
    private const string QUEUE_NAME = "rpc_queue";

    private readonly IConnection connection;
    private readonly IModel channel;
    private readonly string replyQueueName;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> callbackMapper =
        new();

    public RpcClient()
    {
        var factory = new ConnectionFactory { HostName = "localhost" };

        connection = factory.CreateConnection();
        channel = connection.CreateModel();
        // declare a server-named queue
        replyQueueName = channel.QueueDeclare().QueueName;
        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (model, ea) =>
        {
            if (!callbackMapper.TryRemove(ea.BasicProperties.CorrelationId, out var tcs))
                return;
            byte[] imageBytes = ea.Body.ToArray();
            
            string base64String = Convert.ToBase64String(imageBytes);

            tcs.TrySetResult(base64String);
        };

        channel.BasicConsume(consumer: consumer, queue: replyQueueName, autoAck: true);
    }

    public Task<string> CallAsync(string message, CancellationToken cancellationToken = default)
    {
        IBasicProperties props = channel.CreateBasicProperties();
        var correlationId = Guid.NewGuid().ToString();
        props.CorrelationId = correlationId;
        props.ReplyTo = replyQueueName;

        byte[] messageBytes = File.ReadAllBytes(message);

        var tcs = new TaskCompletionSource<string>();
        callbackMapper.TryAdd(correlationId, tcs);

        channel.BasicPublish(
            exchange: string.Empty,
            routingKey: QUEUE_NAME,
            basicProperties: props,
            body: messageBytes
        );

        cancellationToken.Register(() => callbackMapper.TryRemove(correlationId, out _));
        return tcs.Task;
    }

    public void Dispose()
    {
        connection.Close();
    }
}

public class Rpc
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("RPC Client");

        var files = from file in Directory.EnumerateFiles("./img") select file;
        Console.WriteLine("Files: {0}", files.Count<string>().ToString());
        Console.WriteLine("List of Files");
        foreach (var file in files)
        {
            InvokeAsync(file);
        }

        Console.WriteLine(" Press [enter] to exit.");
        Console.ReadLine();
    }

    private static async Task InvokeAsync(string path)
    {
        using var rpcClient = new RpcClient();

        Console.WriteLine(" [x] Requesting Detection on {0}", path);
        string fileName = Path.GetFileName(path);
        string imagePath = $"results/{fileName}";

        string response = await rpcClient.CallAsync(path);
        byte[] imageBytes = Convert.FromBase64String(response);
        File.WriteAllBytes(imagePath, imageBytes);
        Console.WriteLine(" [.] Check results folder for '{0}'", imagePath);
    }
}
