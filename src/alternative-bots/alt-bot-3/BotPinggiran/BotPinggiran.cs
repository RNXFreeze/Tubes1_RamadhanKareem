using System;
using System.Collections.Generic;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;
using System.Linq;


public class BotPinggiran : Bot
{   
    int turnDirection = 1;
    double moveAmount;

    static void Main(string[] args) {
        new BotBarruPattern().Start();
    }

    BotPinggiran() : base(BotInfo.FromFile("BotPinggiran.json")) { }

    public override void Run() {
        turnDirection = 1;
        moveAmount = Math.Max(ArenaWidth, ArenaHeight);
        TurnRight(Direction % 180);
        Forward(moveAmount);

        TurnRight(90);
        TurnGunRight(90);
        while (IsRunning) {
            Forward((ArenaHeight * turnDirection) - 20);
        }
    }


	public override void OnScannedBot(ScannedBotEvent e) {
        Fire(3);
	}

    public override void OnHitBot(HitBotEvent e) {
        turnDirection *= -1;
    }

	public override void OnHitByBullet(HitByBulletEvent e) {
        turnDirection *= -1;
	}
	
	public override void OnHitWall(HitWallEvent e) {
		turnDirection *= -1;
	}
}