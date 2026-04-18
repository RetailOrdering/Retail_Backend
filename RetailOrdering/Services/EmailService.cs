using RetailOrdering.Models;
using System.Net;
using System.Net.Mail;

namespace RetailOrdering.Services;

public interface IEmailService
{
    Task SendOrderConfirmationAsync(string toEmail, Order order);
    Task SendWelcomeEmailAsync(string toEmail, string username);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendOrderConfirmationAsync(string toEmail, Order order)
    {
        var itemsHtml = string.Join("", order.Items.Select(i =>
            $"<tr><td style='padding:8px;border-bottom:1px solid #eee'>{i.Product?.Name ?? $"Product #{i.ProductId}"}</td>" +
            $"<td style='padding:8px;border-bottom:1px solid #eee;text-align:center'>{i.Quantity}</td>" +
            $"<td style='padding:8px;border-bottom:1px solid #eee;text-align:right'>₹{i.UnitPrice:F2}</td>" +
            $"<td style='padding:8px;border-bottom:1px solid #eee;text-align:right'>₹{i.UnitPrice * i.Quantity:F2}</td></tr>"
        ));

        var body = $"""
            <html>
            <body style='font-family:Arial,sans-serif;color:#333;max-width:600px;margin:auto'>
              <div style='background:#FF6B35;padding:20px;text-align:center'>
                <h1 style='color:white;margin:0'>Order Confirmed! 🎉</h1>
              </div>
              <div style='padding:30px'>
                <p>Thank you for your order. Here's your summary:</p>
                <p><strong>Order ID:</strong> #{order.Id}</p>
                <p><strong>Status:</strong> {order.Status}</p>
                <p><strong>Delivery Address:</strong> {order.DeliveryAddress}</p>
                
                <table width='100%' style='border-collapse:collapse;margin:20px 0'>
                  <thead>
                    <tr style='background:#f5f5f5'>
                      <th style='padding:10px;text-align:left'>Item</th>
                      <th style='padding:10px;text-align:center'>Qty</th>
                      <th style='padding:10px;text-align:right'>Price</th>
                      <th style='padding:10px;text-align:right'>Subtotal</th>
                    </tr>
                  </thead>
                  <tbody>{itemsHtml}</tbody>
                  <tfoot>
                    <tr>
                      <td colspan='3' style='padding:10px;text-align:right;font-weight:bold'>Total:</td>
                      <td style='padding:10px;text-align:right;font-weight:bold;color:#FF6B35'>₹{order.TotalAmount:F2}</td>
                    </tr>
                  </tfoot>
                </table>

                <p style='color:#888;font-size:12px'>
                  Ordered on {order.CreatedAt:dd MMM yyyy, hh:mm tt} UTC<br/>
                  If you have any issues, contact our support team.
                </p>
              </div>
            </body>
            </html>
            """;

        await SendEmailAsync(toEmail, $"Order #{order.Id} Confirmed - Retail Ordering", body);
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string username)
    {
        var body = $"""
            <html>
            <body style='font-family:Arial,sans-serif;color:#333;max-width:600px;margin:auto'>
              <div style='background:#FF6B35;padding:20px;text-align:center'>
                <h1 style='color:white;margin:0'>Welcome, {username}! 👋</h1>
              </div>
              <div style='padding:30px'>
                <p>Your account has been created successfully.</p>
                <p>Start browsing our menu of Pizzas, Cold Drinks, and Breads today!</p>
              </div>
            </body>
            </html>
            """;

        await SendEmailAsync(toEmail, "Welcome to Retail Ordering!", body);
    }

    private async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        var smtpSettings = _config.GetSection("SmtpSettings");
        var host = smtpSettings["Host"] ?? "smtp.gmail.com";
        var port = int.Parse(smtpSettings["Port"] ?? "587");
        var username = smtpSettings["Username"]!;
        var password = smtpSettings["Password"]!;
        var fromName = smtpSettings["FromName"] ?? "Retail Ordering";

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(username, password)
        };

        var message = new MailMessage
        {
            From = new MailAddress(username, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(to);

        try
        {
            await client.SendMailAsync(message);
            _logger.LogInformation("Email sent to {Email}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", to);
            // Don't rethrow — email failure should not break core flows
        }
    }
}