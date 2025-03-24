using System;
using System.IO;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;
using Microsoft.Extensions.Configuration;

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
        BodyColor = Color.LightBlue;
        TurretColor = Color.Blue;
        RadarColor = Color.Yellow;
        ScanColor = Color.Orange;

        GunTurnRate = 20.0;
        double time = 0.00;
        double ampl = 30.0;
        double freq = 0.20;
        double safe = 50.0;

        while (IsRunning) {
            if (X < safe) {
                double target = (Y < ArenaHeight / 2) ? 270 : 90;
                TurnToHeading(target);
                SetForward(30);
                Go();
            } else if (X > ArenaWidth - safe) {
                double target = (Y < ArenaHeight / 2) ? 270 : 90;
                TurnToHeading(target);
                SetForward(30);
                Go();
            } else if (Y < safe) {
                double target = (X < ArenaWidth / 2) ? 0 : 180;
                TurnToHeading(target);
                SetForward(30);
                Go();
            } else if (Y > ArenaHeight - safe) {
                double target = (X < ArenaWidth / 2) ? 0 : 180;
                TurnToHeading(target);
                SetForward(30);
                Go();
            } else {
                double tangle = ampl * Math.Sin(time * freq);
                SetTurnRight(tangle);
                SetForward(30);
                Go();
                time += 1.0;
            }
        }
    }

    public override void OnScannedBot(ScannedBotEvent evt) {
        double dis = DistanceTo(evt.X , evt.Y);
        double eng = Energy;
        if (eng < 1.5) {
            return;
        } else if (eng <= 5) {
            Fire(1);
        } else if (eng <= 15) {
            Fire(1.5);
        } else if (eng <= 40) {
            if (dis < 150) {
                Fire(2.5);
            } else {
                Fire(1.5);
            }
        } else {
            if (dis < 100) {
                Fire(3);
            } else if (dis < 200) {
                Fire(2.5);
            } else {
                Fire(1.5);
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
        for (int i = 0; i < 9; i++) {
            SetTurnLeft(10);
            SetForward(10);
            Go();
        }
    }

    private void TurnToHeading(double target) {
        double curdir = Direction;
        double tangle = NormalizeAngle(target - curdir);
        if (tangle < 0) {
            TurnLeft(-tangle);
        } else {
            TurnRight(tangle);
        }
    }

    private double NormalizeAngle(double angle) {
        while (angle > 180) {
            angle -= 360;
        }
        while (angle < -180) {
            angle += 360;
        }
        return angle;
    }
}
