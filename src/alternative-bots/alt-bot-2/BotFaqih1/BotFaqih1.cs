using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;
using System;
using System.Drawing;

public class BotFaqih1 : Bot
{
    int turnCounter;
    bool movingForward;

    static void Main(string[] args)
    {
        new BotFaqih1().Start();
    }

    // Constructor using BotInfo from file
    BotFaqih1() : base(BotInfo.FromFile("BotFaqih1.json")) { }

    // Main execution method for the bot's behavior
    public override void Run()
    {    
        turnCounter = 0;
        movingForward = true;

        GunTurnRate = 45;  // Kecepatan awal radar
        TurnRate = 5;      // Gerakan pola diamond

        while (IsRunning)
        {
            if (turnCounter % 64 == 0) {
                TurnRate = 5; // Belok kiri
                TargetSpeed = movingForward ? 4 : -4;
            }
            if (turnCounter % 64 == 32) {
                TurnRate = -5; // Belok kanan
                TargetSpeed = movingForward ? -6 : 6;
            }
            if (turnCounter % 128 == 0) {
                movingForward    = !movingForward; // Ubah arah gerakan maju-mundur
            }

            turnCounter++;
            Go(); 
        }
    }
    
    // Handle scanned bot events (attacking logic)
    public override void OnScannedBot(ScannedBotEvent e)
    {
        double distance = DistanceTo(e.X, e.Y);
        
            Fire(1);
        
        // Membalik arah putaran radar dengan mengubah tanda dari GunTurnRate
        GunTurnRate = -GunTurnRate;  
    }

    // React to hitting a wall
    public override void OnHitWall(HitWallEvent e)
    {
        TargetSpeed = -1 * TargetSpeed;
    }

    // Handling being hit by a bullet
    public override void OnHitByBullet(HitByBulletEvent e)
    {
       TurnRate = 5;
    }
}
