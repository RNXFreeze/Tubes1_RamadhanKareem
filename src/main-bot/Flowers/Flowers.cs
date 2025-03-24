using System.IO;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

public class Flowers : Bot {
    private bool movingForward = true;

    private Flowers(BotInfo botInfo) : base(botInfo) {
        ;
    }

    static void Main(string[] args) {
        var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("Flowers.json");
        var config = builder.Build();
        var botInfo = BotInfo.FromConfiguration(config);
        new Flowers(botInfo).Start();
    }

    public override void Run() {
        BodyColor = Color.Pink;
        TurretColor = Color.Purple;
        RadarColor = Color.Pink;
        ScanColor = Color.Pink;
        BulletColor = Color.Black;
        GunTurnRate = 50;

        while (IsRunning) {
            MaxSpeed = 8;
            if (movingForward) {
                SetTurnRight(30);
                Forward(500);
            } else {
                SetTurnLeft(30);
                Back(500);
            }
        }
    }

    public override void OnScannedBot(ScannedBotEvent evt) {
        Fire(3);
    }

    public override void OnHitBot(HitBotEvent e) {
        var bearing = BearingTo(e.X , e.Y);
        if (bearing > -10 && bearing < 10) {
            Fire(3);
        }
        if (e.IsRammed) {
            TurnLeft(10);
            movingForward = !movingForward;
        }
    }

    public override void OnHitByBullet(HitByBulletEvent e) {
        movingForward = !movingForward;
    }
}
