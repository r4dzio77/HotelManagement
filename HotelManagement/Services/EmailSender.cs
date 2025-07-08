using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace HotelManagement.Services
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string email, string subject, string htmlMessage);
    }

    public class EmailSender : IEmailSender
    {
        private readonly string _smtpHost = "smtp.gmail.com";
        private readonly int _smtpPort = 587;
        private readonly string _smtpUser = "hotelmanagement.biuro@gmail.com"; // Podmień na swój email
        private readonly string _smtpPass = "gqvaeshzgpxoqfvx"; // Podmień na hasło aplikacji

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var mail = new MailMessage();
            mail.To.Add(email);
            mail.From = new MailAddress(_smtpUser);
            mail.Subject = subject;
            mail.Body = htmlMessage;
            mail.IsBodyHtml = true;

            using var smtp = new SmtpClient(_smtpHost, _smtpPort)
            {
                Credentials = new NetworkCredential(_smtpUser, _smtpPass),
                EnableSsl = true
            };

            await smtp.SendMailAsync(mail);
        }
    }
}
