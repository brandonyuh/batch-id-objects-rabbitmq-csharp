using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Emgu.CV.Dnn;

var factory = new ConnectionFactory { HostName = "localhost" };
using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

channel.QueueDeclare(
    queue: "rpc_queue",
    durable: false,
    exclusive: false,
    autoDelete: false,
    arguments: null
);
channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
var consumer = new EventingBasicConsumer(channel);
channel.BasicConsume(queue: "rpc_queue", autoAck: false, consumer: consumer);
Console.WriteLine(" [x] Awaiting RPC requests");

consumer.Received += (model, ea) =>
{
    string response = string.Empty;

    var body = ea.Body.ToArray();
    var props = ea.BasicProperties;
    var replyProps = channel.CreateBasicProperties();
    replyProps.CorrelationId = props.CorrelationId;
    string processedImagePath = String.Empty;

    try
    {
        byte[] imageBytes = ea.Body.ToArray();
        Guid myuuid = Guid.NewGuid();
        string imagePath = $"img/{myuuid}.jpg";
        File.WriteAllBytes(imagePath, imageBytes);

        processedImagePath = DrawDetectionBoxes(imagePath);
    }
    catch (Exception e)
    {
        Console.WriteLine($" [.] {e.Message}");
        response = string.Empty;
    }
    finally
    {
        byte[] responseBytes = File.ReadAllBytes(processedImagePath);

        channel.BasicPublish(
            exchange: string.Empty,
            routingKey: props.ReplyTo,
            basicProperties: replyProps,
            body: responseBytes
        );
        channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
    }
};

Console.WriteLine(" Press [enter] to exit.");
Console.ReadLine();

static String DrawDetectionBoxes(string imagePath)
{
    Mat image = CvInvoke.Imread(imagePath);

    return imagePath;
}
