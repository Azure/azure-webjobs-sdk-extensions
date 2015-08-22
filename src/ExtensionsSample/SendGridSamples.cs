using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions;

using SendGridMessage = SendGrid.SendGridMessage;

namespace ExtensionsSample
{
    // { "OrderId": "12345", "CustomerName": "John Doe", "CustomerEmail": "john@johndoe.net" }
    public class Order
    {
        public string OrderId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
    }

    public static class SendGridSamples
    {
        /// <summary>
        /// Demonstrates declaratively defining all email properties with parameter binding
        /// to message properties.
        /// </summary>
        public static void ProcessOrder_Declarative(
            [QueueTrigger(@"samples-orders")] Order order,
            [SendGrid(
                To = "{CustomerEmail}",
                Subject = "Thanks for your order (#{OrderId})!",
                Text = "{CustomerName}, we've received your order ({OrderId}) and have begun processing it!")]
            SendGridMessage message)
        {
            // You can set additional message properties here
        }

        /// <summary>
        /// Demonstrates imperatively defining all email properties in the function body.
        /// </summary>
        public static void ProcessOrder_Imperative(
            [QueueTrigger(@"samples-orders")] Order order,
            [SendGrid] SendGridMessage message)
        {
            message.AddTo(order.CustomerEmail);
            message.Subject = string.Format("Thanks for your order (#{0})!", order.OrderId);
            message.Text = string.Format("{0}, we've received your order ({1}) and have begun processing it!", order.CustomerName, order.OrderId);
        }
    }
}
