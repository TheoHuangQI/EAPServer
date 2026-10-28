using Apache.NMS;
using Apache.NMS.ActiveMQ;
using EAPManage.Models;
using EAPManage.Monitor;
using EAPManage.Utils;
using FluentScheduler;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});


builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// ���� CORS������ǰ�˷��ʣ�
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});





// ���� SignalR ����
builder.Services.AddSignalR();
builder.Services.AddScoped<LogService>();
var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("AllowAll");
app.UseHttpsRedirection();
// ���� SignalR �ս��
app.MapHub<LogHub>("/logHub");
app.UseAuthorization();

app.MapControllers();

#region MQ��ʼ��
ActiveMQClient activeMQClient = ActiveMQClient.Current;
// ע���¼�
activeMQClient.ConnectionInterrupted += () => Console.WriteLine("Connection lost");
activeMQClient.ConnectionRestored += () => Console.WriteLine("Connection restored");
activeMQClient.PermanentFailure += () => Console.WriteLine("Permanent failure");
// ʹ����Ϣ���͹�����
var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();
MessageProcessor messageProcessor = new MessageProcessor(scopeFactory);
activeMQClient.MessageReceived += (s, e) =>
{
    switch (e.Message)
    {
        case ITextMessage textMsg:
            messageProcessor.HandleMessage(textMsg, e);
            break;
        case IMapMessage mapMsg:
            messageProcessor.HandleMessage(mapMsg, e);
            break;
        case IBytesMessage bytesMsg:
            messageProcessor.HandleMessage(bytesMsg, e);
            break;
    }
};
await activeMQClient.InitializeAsync();
//_ = activeMQClient.InitializeAsync();//MQûװ�Ļ����������д��룬�������ע�͵�
#endregion MQ��ʼ��

#region ����Monitor
var registry = new Registry();
//var accessModeMonitor = new AccessModeMonitor();
//var controlStateMonitor = new ControlStateMonitor();
var eapMonitor = new EAPMonitor(scopeFactory);
//var eqpMonitor = new EQPMonitor(scopeFactory);
//var transferStatusMonitor = new TransferStatusMonitor();
//registry.Schedule(accessModeMonitor).ToRunNow().AndEvery(30).Seconds();
//registry.Schedule(controlStateMonitor).ToRunNow().AndEvery(30).Seconds();
registry.Schedule(eapMonitor).ToRunNow().AndEvery(10).Seconds();
//registry.Schedule(eqpMonitor).ToRunNow().AndEvery(30).Seconds();
//registry.Schedule(transferStatusMonitor).ToRunNow().AndEvery(30).Seconds();
JobManager.Initialize(registry);
#endregion
app.Run();





