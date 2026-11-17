using System.Net;
using System.Net.Sockets;
using System.Text;
using SurveillanceAI.Application;
using SurveillanceAI.Domain;
using SurveillanceAI.Infrastructure;
using Xunit;

namespace SurveillanceAI.IntegrationTests;
public sealed class SmtpTests
{
    [Fact] public async Task MailKitDeliversToLoopbackSmtpWithoutExternalEmail()
    {
        using var listener=new TcpListener(IPAddress.Loopback,0); listener.Start(); var port=((IPEndPoint)listener.LocalEndpoint).Port;
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var received=new StringBuilder();
        var server=Task.Run(async()=>
        {
            using var client=await listener.AcceptTcpClientAsync(timeout.Token);await using var stream=client.GetStream();
            using var reader=new StreamReader(stream,Encoding.ASCII,leaveOpen:true);await using var writer=new StreamWriter(stream,Encoding.ASCII,leaveOpen:true){NewLine="\r\n",AutoFlush=true};
            await writer.WriteLineAsync("220 localhost test SMTP");var data=false;
            while(await reader.ReadLineAsync(timeout.Token) is {} line)
            {
                if(data){if(line=="."){data=false;await writer.WriteLineAsync("250 queued");}else received.AppendLine(line);continue;}
                if(line.StartsWith("EHLO")){await writer.WriteLineAsync("250-localhost");await writer.WriteLineAsync("250 SIZE 1048576");}
                else if(line=="DATA"){data=true;await writer.WriteLineAsync("354 End with dot");}
                else if(line=="QUIT"){await writer.WriteLineAsync("221 bye");break;}
                else await writer.WriteLineAsync("250 OK");
            }
        },timeout.Token);
        var transport=new MailKitTransport(new DpapiProtector());
        await transport.SendAsync(new SmtpSettings{Host="127.0.0.1",Port=port,TlsMode="LocalTest",Sender="sender@example.invalid"},new NotificationDelivery{Address="recipient@example.invalid",Subject="Local integration test",Body="Potential detection requiring operator review."},timeout.Token);
        await server.WaitAsync(timeout.Token); Assert.Contains("Local integration test",received.ToString()); Assert.Contains("operator review",received.ToString());
    }
    [Fact] public async Task InsecureSmtpIsRejectedOutsideLoopback()
    {
        var transport=new MailKitTransport(new DpapiProtector());
        await Assert.ThrowsAsync<ArgumentException>(()=>transport.SendAsync(new SmtpSettings{Host="example.invalid",TlsMode="LocalTest",Sender="sender@example.invalid"},new NotificationDelivery{Address="recipient@example.invalid"},default));
    }
}
