using System;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

public class Flows : Bot {
    int turnCounter;
    bool movingForward;

    static void Main(string[] args) {
        new Flows().Start();
    }

    Flows() : base(BotInfo.FromFile("Flows.json")) {
        ;
    }

    public override void Run() {
        BodyColor = Color.Purple;
        TurretColor = Color.Blue;
        RadarColor = Color.Cyan;
        BulletColor = Color.Black;
        ScanColor = Color.Purple;
         
        turnCounter = 0;
        movingForward = true;
        GunTurnRate = 45;
        TurnRate = 5;

        while (IsRunning) {
            if (turnCounter % 64 == 0) {
                TurnRate = 5;
                TargetSpeed = movingForward ? 4 : -4;
            }
            if (turnCounter % 64 == 32) {
                TurnRate = -5;
                TargetSpeed = movingForward ? -6 : 6;
            }
            if (turnCounter % 128 == 0) {
                movingForward = !movingForward;
            }
            turnCounter++;
            Go(); 
        }
    }
    
    public override void OnScannedBot(ScannedBotEvent e) {
        double dis = DistanceTo(e.X, e.Y);
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
        GunTurnRate = -GunTurnRate;  
    }

    public override void OnHitWall(HitWallEvent e) {
        TargetSpeed = -TargetSpeed;
    }

    public override void OnHitByBullet(HitByBulletEvent e) {
       TurnRate = 5;
    }
}
