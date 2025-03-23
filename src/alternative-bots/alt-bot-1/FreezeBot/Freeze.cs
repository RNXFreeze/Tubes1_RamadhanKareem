using System;
using System.IO;
using System.Drawing;
using Microsoft.Extensions.Configuration;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

public class Freeze : Bot {
    private Freeze(BotInfo botInfo) : base(botInfo) {
        ;
    }

    public static void Main(string[] args) {
        var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("Freeze.json");
        var config = builder.Build();
        var botInfo = BotInfo.FromConfiguration(config);
        new Freeze(botInfo).Start();
    }

    public override void Run() {
        BodyColor   = Color.LightBlue;
        TurretColor = Color.Blue;
        RadarColor  = Color.Yellow;
        ScanColor   = Color.Orange;

        GunTurnRate = 20;
        double time = 0.0;
        double amplitude = 30.0;
        double frequency = 0.20;

        while (IsRunning) {
            double turnAngle = amplitude * Math.Sin(time * frequency);
            SetTurnRight(turnAngle);
            SetForward(30);
            Go();
            time += 1.0;
        }
    }

    public override void OnScannedBot(ScannedBotEvent evt) {
        double distance = DistanceTo(evt.X , evt.Y);
        double energy = Energy;
        if (energy < 1.5) {
            return;
        } else if (energy <= 10) {
            Fire(1);
        } else if (energy <= 30) {
            if (distance < 200) {
                Fire(2);
            } else {
                Fire(1);
            }
        } else {
            if (distance < 150) {
                Fire(3);
            } else if (distance < 300) {
                Fire(2);
            } else {
                Fire(1);
            }
        }
    }

    public override void OnHitWall(HitWallEvent e) {
        Back(90);
        TurnLeft(135);
        Forward(90);
    }

    public override void OnHitBot(HitBotEvent e) {
        Back(60);
        TurnRight(30);
        Forward(60);
    }

    public override void OnHitByBullet(HitByBulletEvent e) {
        for (int i = 0 ; i < 9 ; i++) {
            SetTurnLeft(10);
            SetForward(10);
            Go();
        }
    }
}
