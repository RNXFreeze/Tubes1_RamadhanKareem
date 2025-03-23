using System;
using System.Collections.Generic;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;
using System.Drawing;

public class BotFaqih2 : Bot
{
    // Konstanta
    public const double BATTLEFIELD_MARGIN = 50;
    public const double MAX_FIREPOWER = 3.0;
    public const double MIN_FIREPOWER = 1.0;
    public const double WALL_AVOID_DISTANCE = 100;
    
    // Variabel pelacakan target
    public double enemyDistance;
    public double enemyBearing;
    public double enemyEnergy;
    public double enemyX;
    public double enemyY;
    public double enemyHeading;
    public double enemySpeed;
    public long lastEnemyScan = 0;
    
    // Penyimpanan posisi terakhir yang diketahui dari musuh
    public Dictionary<int, ScannedBotEvent> enemyMap = new Dictionary<int, ScannedBotEvent>();
    
    // Status mode pertarungan
    public bool isHuntingMode = false;
    public bool isWallFollowingMode = false;
    public bool isEvadingMode = false;
    public int moveDirection = 1;
    public int gunDirection = 1;
    public int wallFollowDirection = 1;
    public int scanDirection = 1;
    
    // Tracking lawan terakhir yang ditembak
    public int lastTargetId = -1;

    static void Main(string[] args)
    {
        new BotFaqih2().Start();
    }
    
    public BotFaqih2() : base(BotInfo.FromFile("BotFaqih2.json")) { }
    
    public override void Run()
    {
        // Inisialisasi bot
        TurnRadarRight(360); // Radar berputar terus
        
        while (IsRunning)
        {
            // Strategi default - cari musuh
            if (lastEnemyScan < TurnNumber - 5) // Belum melihat musuh selama 5 putaran
            {
                ScanForEnemies();
            }
            else if (isEvadingMode)
            {
                EvadeEnemy();
            }
            else if (isWallFollowingMode)
            {
                FollowWalls();
            }
            else if (isHuntingMode)
            {
                HuntEnemy();
            }
            else
            {
                // Gerakan default - zigzag
                PerformZigzagMovement();
            }
            
            Go(); // Eksekusi semua perintah yang tertunda
        }
    }
    
    // Event saat radar mendeteksi bot lain
    public override void OnScannedBot(ScannedBotEvent e)
    {
        lastEnemyScan = TurnNumber;
        
        // Simpan informasi musuh
        enemyDistance = CalculateDistance(X, Y, e.X, e.Y);
        enemyBearing = CalculateBearing(X, Y, Direction, e.X, e.Y);
        enemyEnergy = e.Energy;
        enemyX = e.X;
        enemyY = e.Y;
        enemyHeading = e.Direction;
        enemySpeed = e.Speed;
        
        // Menyimpan data enemy ke map
        enemyMap[e.ScannedBotId] = e;
        lastTargetId = e.ScannedBotId;
        
        // Radar lock - Narrow lock dengan oscillation kecil
        SetTurnRadarLeft(GetRadarTurnAngleToTarget(e.X, e.Y) * 1.2);
        
        // Putar senjata ke arah musuh
        double turnAngle = GetGunTurnAngleToTarget(e.X, e.Y);
        SetTurnGunLeft(turnAngle);
        
        // Pilih strategi berdasarkan kondisi
        ChooseBattleStrategy(e);
        
        // Tembak dengan kekuatan yang sesuai dengan jarak
        if (Math.Abs(turnAngle) < 10 && GunHeat == 0)
        {
            double firepower = CalculateOptimalFirepower(enemyDistance, enemySpeed);
            Fire(firepower);
        }
    }
    
    // Event saat terkena tembakan
    public override void OnHitByBullet(HitByBulletEvent e)
    {
        isEvadingMode = true;
        
        // Putar dan hindari arah datangnya peluru
        double bearingFromBullet = CalculateBearing(X, Y, Direction, e.Bullet.X, e.Bullet.Y);
        SetTurnRight(90 - bearingFromBullet);
        
        // Balik arah gerakan
        moveDirection *= -1;
        SetForward(100 * moveDirection);
    }
    
    // Event saat menabrak dinding
    public override void OnHitWall(HitWallEvent e)
    {
        isWallFollowingMode = true;
        moveDirection *= -1;
        
        // Putar menjauh dari dinding
        SetTurnRight(90);
        SetForward(100 * moveDirection);
    }
    
    // Event saat menabrak bot lain
    public override void OnHitBot(HitBotEvent e)
    {
        // Jika menabrak dari depan, tembak dengan kekuatan maksimal
        if (e.IsRammed)
        {
            double bearingToBot = CalculateBearing(X, Y, Direction, e.X, e.Y);
            SetTurnGunLeft(GetGunTurnAngleToTarget(e.X, e.Y));
            Fire(MAX_FIREPOWER);
            SetTurnRight(bearingToBot);
            SetBack(100);
        }
        else
        {
            // Jika tertabrak dari belakang, putar dan serang
            double bearingToBot = CalculateBearing(X, Y, Direction, e.X, e.Y);
            SetTurnRight(bearingToBot);
            SetForward(100);
        }
    }
    
    // Metode untuk mencari musuh
    public void ScanForEnemies()
    {
        // Jelajahi arena dengan pergerakan acak
        if (TurnNumber % 20 == 0)
        {
            SetTurnRight(60 * (new Random().NextDouble() < 0.5 ? 1 : -1));
        }
        
        // Radar berputar penuh untuk mencari musuh
        scanDirection *= -1;
        SetTurnRadarRight(360 * scanDirection);
        
        // Pergerakan maju untuk menjelajahi arena
        SetForward(100);
    }
    
    // Metode untuk "memburu" musuh
    public void HuntEnemy()
    {
        if (enemyMap.ContainsKey(lastTargetId))
        {
            ScannedBotEvent lastTarget = enemyMap[lastTargetId];
            
            // Prediksi gerakan musuh
            double futureX = PredictFutureX(lastTarget);
            double futureY = PredictFutureY(lastTarget);
            
            // Putar ke arah prediksi
            double bearingToTarget = CalculateBearing(X, Y, Direction, futureX, futureY);
            SetTurnRight(bearingToTarget);
            
            // Putar senjata ke arah prediksi
            SetTurnGunLeft(GetGunTurnAngleToTarget(futureX, futureY));
            
            // Gerakan agresif - mendekati musuh
            if (enemyDistance > 200)
            {
                SetForward(100);
            }
            else
            {
                // Pertahankan jarak menengah yang optimal
                PerformOrbitalMovement();
            }
        }
    }
    
    // Metode untuk mengikuti dinding
    public void FollowWalls()
    {
        // Cek jarak ke dinding
        double distanceToNorthWall = ArenaHeight - Y;
        double distanceToSouthWall = Y;
        double distanceToEastWall = ArenaWidth - X;
        double distanceToWestWall = X;
        
        double minWallDistance = Math.Min(
            Math.Min(distanceToNorthWall, distanceToSouthWall),
            Math.Min(distanceToEastWall, distanceToWestWall)
        );
        
        // Jika sudah cukup jauh dari dinding, kembali ke mode normal
        if (minWallDistance > WALL_AVOID_DISTANCE)
        {
            isWallFollowingMode = false;
            return;
        }
        
        // Berada di dekat dinding, putar sejajar dengan dinding
        if (distanceToNorthWall < WALL_AVOID_DISTANCE || distanceToSouthWall < WALL_AVOID_DISTANCE)
        {
            // Dekat dengan dinding utara/selatan, gerakan timur/barat
            SetTurnRight(90 * wallFollowDirection - Direction);
        }
        else
        {
            // Dekat dengan dinding timur/barat, gerakan utara/selatan
            SetTurnRight(wallFollowDirection * (Direction < 180 ? 90 : -90));
        }
        
        // Bergerak maju sejajar dengan dinding
        SetForward(100);
        
        // Secara periodik ganti arah gerakan di sepanjang dinding
        if (TurnNumber % 50 == 0)
        {
            wallFollowDirection *= -1;
        }
    }
    
    // Metode untuk menghindari serangan
    public void EvadeEnemy()
    {
        // Hanya bertahan dalam mode menghindar untuk beberapa putaran
        if (TurnNumber % 10 == 0)
        {
            isEvadingMode = false;
            return;
        }
        
        // Gerakan zigzag cepat dengan jarak pendek
        if (TurnNumber % 5 == 0)
        {
            moveDirection *= -1;
            gunDirection *= -1;
            
            // Putar tegak lurus dari musuh
            SetTurnRight(enemyBearing + 90 * moveDirection);
            SetForward(50 * moveDirection);
            
            // Putar senjata tetap ke arah musuh
            if (enemyX != 0 && enemyY != 0)
            {
                SetTurnGunLeft(GetGunTurnAngleToTarget(enemyX, enemyY));
            }
        }
    }
    
    // Gerakan orbital di sekitar target
    public void PerformOrbitalMovement()
    {
        // Mempertahankan jarak dari musuh sambil berputar di sekitarnya
        SetTurnRight(enemyBearing + 75 * moveDirection);
        SetForward(50 * moveDirection);
        
        // Ubah arah putaran secara periodik
        if (TurnNumber % 20 == 0 || Math.Abs(DistanceRemaining) < 10)
        {
            moveDirection *= -1;
        }
    }
    
    // Gerakan zigzag default
    public void PerformZigzagMovement()
    {
        // Ganti arah secara periodik
        if (TurnNumber % 10 == 0 || Math.Abs(DistanceRemaining) < 10)
        {
            moveDirection *= -1;
            SetTurnRight(45 * moveDirection);
            SetForward(100 * moveDirection);
        }
        
        // Putar radar untuk terus mencari
        if (TurnNumber % 20 == 0)
        {
            scanDirection *= -1;
            SetTurnRadarRight(360 * scanDirection);
        }
    }
    
    // Metode untuk memilih strategi pertarungan
    public void ChooseBattleStrategy(ScannedBotEvent e)
    {
        // Reset status mode
        if (TurnNumber % 30 == 0)
        {
            isEvadingMode = false;
            isWallFollowingMode = false;
        }
        
        // Deteksi jika musuh menembak (penurunan energi antara 0.1 dan 3.0)
        double energyDrop = 0;
        if (enemyMap.ContainsKey(e.ScannedBotId))
        {
            ScannedBotEvent previousScan = enemyMap[e.ScannedBotId];
            energyDrop = previousScan.Energy - e.Energy;
        }
        
        // Jika musuh menembak, hindari
        if (energyDrop >= 0.1 && energyDrop <= 3.0)
        {
            isEvadingMode = true;
            isHuntingMode = false;
        }
        // Jika dekat dengan dinding, aktivasi mode mengikuti dinding
        else if (IsNearWall())
        {
            isWallFollowingMode = true;
            isHuntingMode = false;
        }
        // Jika musuh lemah, serang agresif
        else if (e.Energy < 30)
        {
            isHuntingMode = true;
        }
        // Jika jarak dekat, aktifkan mode orbital
        else if (enemyDistance < 150)
        {
            isHuntingMode = true;
        }
        // Default mode zigzag
        else
        {
            isHuntingMode = enemyDistance < 300; // Aktifkan mode berburu hanya jika cukup dekat
        }
    }
    
    // Cek apakah bot berada dekat dengan dinding
    public bool IsNearWall()
    {
        return X < BATTLEFIELD_MARGIN || Y < BATTLEFIELD_MARGIN || 
               X > ArenaWidth - BATTLEFIELD_MARGIN || 
               Y > ArenaHeight - BATTLEFIELD_MARGIN;
    }
    
    // Hitung jarak antara dua titik
    public double CalculateDistance(double x1, double y1, double x2, double y2)
    {
        return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
    }
    
    // Hitung arah relatif ke suatu titik
    public double CalculateBearing(double botX, double botY, double botHeading, double targetX, double targetY)
    {
        double dx = targetX - botX;
        double dy = targetY - botY;
        
        // Hitung sudut absolut ke target
        double angle = Math.Atan2(dy, dx) * (180 / Math.PI);
        
        // Konversi ke bearing relatif
        double bearing = angle - botHeading;
        
        // Normalisasi ke -180 hingga 180
        while (bearing > 180) bearing -= 360;
        while (bearing < -180) bearing += 360;
        
        return bearing;
    }
    
    // Hitung sudut putaran radar untuk target
    public double GetRadarTurnAngleToTarget(double targetX, double targetY)
    {
        double dx = targetX - X;
        double dy = targetY - Y;
        
        // Sudut absolut ke target
        double absoluteBearing = Math.Atan2(dy, dx) * (180 / Math.PI);
        
        // Sudut radar saat ini
        double radarHeading = Direction + RadarDirection;
        
        // Normalisasi ke -180 hingga 180
        double radarTurnAngle = absoluteBearing - radarHeading;
        while (radarTurnAngle > 180) radarTurnAngle -= 360;
        while (radarTurnAngle < -180) radarTurnAngle += 360;
        
        return radarTurnAngle;
    }
    
    // Hitung sudut putaran senjata untuk target
    public double GetGunTurnAngleToTarget(double targetX, double targetY)
    {
        double dx = targetX - X;
        double dy = targetY - Y;
        
        // Sudut absolut ke target
        double absoluteBearing = Math.Atan2(dy, dx) * (180 / Math.PI);
        
        // Sudut senjata saat ini
        double gunHeading = Direction + GunDirection;
        
        // Normalisasi ke -180 hingga 180
        double gunTurnAngle = absoluteBearing - gunHeading;
        while (gunTurnAngle > 180) gunTurnAngle -= 360;
        while (gunTurnAngle < -180) gunTurnAngle += 360;
        
        return gunTurnAngle;
    }
    
    // Hitung kekuatan tembakan optimal
    public double CalculateOptimalFirepower(double distance, double speed)
    {
        // Algoritma sederhana untuk menghitung firepower
        // Jarak dekat = tembakan kuat, jarak jauh = tembakan lemah
        double power = MAX_FIREPOWER - (distance / 150.0);
        
        // Jika target bergerak cepat, kurangi kekuatan untuk meningkatkan laju tembakan
        power -= (speed / 16.0);
        
        // Batasi ke rentang yang valid
        return Math.Max(MIN_FIREPOWER, Math.Min(MAX_FIREPOWER, power));
    }
    
    // Prediksi posisi X musuh di masa depan
    public double PredictFutureX(ScannedBotEvent e)
    {
        double headingRadians = e.Direction * (Math.PI / 180);
        return e.X + Math.Sin(headingRadians) * e.Speed * 2;
    }
    
    // Prediksi posisi Y musuh di masa depan
    public double PredictFutureY(ScannedBotEvent e)
    {
        double headingRadians = e.Direction * (Math.PI / 180);
        return e.Y + Math.Cos(headingRadians) * e.Speed * 2;
    }
}