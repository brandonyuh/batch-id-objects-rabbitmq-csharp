using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var factory = new ConnectionFactory { HostName = "localhost" };
using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

channel.QueueDeclare(queue: "rpc_queue",
                     durable: false,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);
channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
var consumer = new EventingBasicConsumer(channel);
channel.BasicConsume(queue: "rpc_queue",
                     autoAck: false,
                     consumer: consumer);
Console.WriteLine(" [x] Awaiting RPC requests");

consumer.Received += (model, ea) =>
{
    string response = string.Empty;

    var body = ea.Body.ToArray();
    var props = ea.BasicProperties;
    var replyProps = channel.CreateBasicProperties();
    replyProps.CorrelationId = props.CorrelationId;

    try
    {
        var message = Encoding.UTF8.GetString(body);
        int n = int.Parse(message);
        Console.WriteLine($" [.] Fib({message})");
        response = AddOne(n).ToString();
    }
    catch (Exception e)
    {
        Console.WriteLine($" [.] {e.Message}");
        response = string.Empty;
    }
    finally
    {
        var responseBytes = Encoding.UTF8.GetBytes(response);
        channel.BasicPublish(exchange: string.Empty,
                             routingKey: props.ReplyTo,
                             basicProperties: replyProps,
                             body: responseBytes);
        channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
    }
};

Console.WriteLine(" Press [enter] to exit.");
Console.ReadLine();

static int AddOne(int n)
{
    return n+1;
}