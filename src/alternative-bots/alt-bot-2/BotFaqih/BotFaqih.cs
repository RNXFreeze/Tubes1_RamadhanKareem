using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;
using System;
using System.Collections.Generic;
using System.Linq;

public class BotFaqih : Bot
{
    int turnCounter;
    bool movingForward;
    const double SAFE_DISTANCE = 300; // Jarak aman dari bot lain yang diperbesar

    static void Main(string[] args)
    {
        new BotFaqih().Start();
    }

    // Constructor using BotInfo from file
    BotFaqih() : base(BotInfo.FromFile("BotFaqih.json")) { }

    public override void Run()
    {    
        turnCounter = 0;
        movingForward = true;

        GunTurnRate = 20;  // Kecepatan radar menengah untuk scanning lebih sering

        while (IsRunning)
        {
            // Minimal Risk Movement Calculation
            double safestAngle = CalculateSafestAngle();
            TurnRate = (safestAngle - Direction) % 360;

            if (IsMovingTowardEnemy(safestAngle))
            {
                movingForward = false; // Balik arah kalau mendekati musuh
            }

            TargetSpeed = movingForward ? 8 : -8;

            turnCounter++;
            Go(); 
        }
    }

    private double CalculateSafestAngle()
    {
        List<double> risks = new List<double>();
        List<double> angles = new List<double>();

        for (int i = 0; i < 360; i += 10)
        {
            double angle = i;
            double risk = 0;

            foreach (var bot in GetAllEnemies())
            {
                double distance = DistanceTo(bot.X, bot.Y);
                double angleToBot = BearingTo(bot.X, bot.Y);
                double angleDifference = Math.Abs(angle - angleToBot);

                if (angleDifference > 180) angleDifference = 360 - angleDifference;

                // Risiko lebih tinggi jika lebih dekat atau berada dalam sudut yang mengarah ke bot musuh
                double distanceRisk = Math.Max(1, (SAFE_DISTANCE - distance));
                if (distance < SAFE_DISTANCE)
                {
                    distanceRisk *= 2; // Gandakan risiko jika terlalu dekat
                }
                risk += distanceRisk * (1 + (angleDifference / 180));
            }

            angles.Add(angle);
            risks.Add(risk);
        }

        // Pilih sudut dengan risiko terendah
        double safestAngle = angles[risks.IndexOf(risks.Min())];
        return safestAngle;
    }

    private bool IsMovingTowardEnemy(double safestAngle)
    {
        foreach (var bot in GetAllEnemies())
        {
            double distance = DistanceTo(bot.X, bot.Y);
            if (distance < SAFE_DISTANCE)
            {
                double angleToBot = BearingTo(bot.X, bot.Y);
                double angleDifference = Math.Abs(safestAngle - angleToBot);
                if (angleDifference < 45) return true; // Jika bergerak ke arah musuh
            }
        }
        return false;
    }

    public override void OnScannedBot(ScannedBotEvent e)
    {
        if (e.Distance <= SAFE_DISTANCE)
        {
            movingForward = false; // Mengubah arah agar menjauh dari musuh
        }
        else
        {
            movingForward = true;
        }

        Fire(1); // Menembak setiap musuh yang terdeteksi dengan peluru kecil

        GunTurnRate = -GunTurnRate;  // Membalik arah putaran radar agar tidak terkunci pada satu musuh
    }

    public override void OnHitWall(HitWallEvent e)
    {
        movingForward = !movingForward; // Mengubah arah gerakan
    }

    public override void OnHitByBullet(HitByBulletEvent e)
    {
        TurnRate = 5; // Mengubah arah sedikit saat terkena peluru
    }

    private List<ScannedBotEvent> GetAllEnemies()
    {
        List<ScannedBotEvent> enemies = new List<ScannedBotEvent>();

        foreach (var botEvent in Events)
        {
            if (botEvent is ScannedBotEvent scannedBot)
            {
                enemies.Add(scannedBot);
            }
        }

        return enemies;
    }
}
