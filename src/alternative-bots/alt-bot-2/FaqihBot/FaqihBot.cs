using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;

/// <summary>
/// BorderGuard - A C# implementation of the BorderGuard robot for Robocode Tank Royale.
/// This robot demonstrates how to create a bot that guards the border area of the battle field,
/// efficient against robots trying to hide at corners and sneaking around near the borders.
/// 
/// This bot is somewhat advanced due to:
/// 1) It uses linear targeting for predicting how to hit robots that move in straight lines
/// 2) It only fires at a robot if its bullets will do damage to that robot (when the predicted position is within attack range)
/// 3) It has an efficient scanner that keeps the scan angle as minimal as possible for fresh data
/// 4) It picks the nearest robot as a target that it can damage through prediction
/// 5) It only moves along the borders of the battle field and constantly changes direction
/// 
/// Based on the Java implementation for Robocode by Flemming N. Larsen
/// Adapted for Robocode Tank Royale in C#
/// </summary>
public class BorderGuard : Bot
{
    // Constants
    private const double FIREPOWER = 3; // Max power => violent as this robot can afford it!
    private const double HALF_BOT_SIZE = 18; // Bot size is 36x36 units, half size is 18 units
    int Time = Environment.TickCount;
    // Map containing data for all scanned robots
    // The key is a robot ID and the value is an object containing robot data
    private readonly Dictionary<int, RobotData> enemyMap;
    
    // Scanning direction (positive = right, negative = left)
    private double scanDir = 1;
    
    // Oldest scanned robot. Can be null.
    private RobotData oldestScanned;
    
    // Target robot for the gun. Can be null meaning there is no current target.
    private RobotData target;
    
    // Last time when the robot shifted its direction
    private long lastDirectionShift;
    
    // Current direction, 1 = forward, -1 = backward
    private int direction = 1;
    
    // Border sentry size - equivalent to the getSentryBorderSize() in the Java version
    private double sentryBorderSize;

    static void Main(string[] args)
    {
        new BorderGuard().Start();
    }

    /// <summary>
    /// Constructs this robot.
    /// </summary>
    public BorderGuard() : base(BotInfo.FromFile("BorderGuard.json"))
    {
        // Initialize the dictionary to store robot data
        enemyMap = new Dictionary<int, RobotData>();
        
        // Set a default sentry border size (can be adjusted based on arena size)
        sentryBorderSize = 100;
    }
    
    /// <summary>
    /// Main method that's called when the robot starts a round.
    /// </summary>
    public override void Run()
    {
        // Do initialization stuff before the loop
        Initialize();
        
        // Loop forever. The bot must take action or the game will disable it!
        while (IsRunning)
        {
            // Handle a single turn
            
            // Handle the radar that scans for enemy robots
            HandleRadar();
            
            // Handle the gun by turning it and firing at our target
            HandleGun();
            
            // Move the robot around on the battlefield
            MoveRobot();
            
            // Execute all pending commands for the next turn
            Go();
        }
    }
    
    /// <summary>
    /// This method is called when the robot sees another robot.
    /// </summary>
    public override void OnScannedBot(ScannedBotEvent e)
    {
        // Update the enemy map
        UpdateEnemyMap(e);
        
        // Update the scan direction
        UpdateScanDirection(e);
        
        // Update enemy target positions
        UpdateEnemyTargetPositions();
    }
    
    /// <summary>
    /// This method is called when another robot dies.
    /// </summary>
    public override void OnBotDeath(BotDeathEvent e)
    {
        // Get the id of the robot that died
        int deadRobotId = e.VictimId;
        
        // Remove the robot data for the robot that died from the enemy map
        enemyMap.Remove(deadRobotId);
        
        // Remove the data entry for the oldest scanned robot if we have such an entry
        if (oldestScanned != null && oldestScanned.id == deadRobotId)
        {
            oldestScanned = null;
        }
        
        if (target != null && target.id == deadRobotId)
        {
            target = null;
        }
    }
    
    /// <summary>
    /// Initializes this robot before a new round in a battle.
    /// </summary>
    private void Initialize()
    {
        // Set radar and gun to turn independently
        // Note: In Tank Royale, this is handled differently than in classic Robocode
        
        // Set initial scan rate
        RadarTurnRate = 45; // Start with a moderate turn rate
        
        // Set colors
        BodyColor = System.Drawing.Color.FromArgb(0x5C, 0x33, 0x17); // Chocolate Brown
        GunColor = System.Drawing.Color.FromArgb(0x45, 0x8B, 0x74);  // Aqua Marine
        RadarColor = System.Drawing.Color.FromArgb(0xD2, 0x69, 0x1E); // Orange Chocolate
        BulletColor = System.Drawing.Color.FromArgb(0xFF, 0xD3, 0x9B); // Burly wood
        ScanColor = System.Drawing.Color.FromArgb(0xCA, 0xFF, 0x70);  // Olive Green
    }
    
    /// <summary>
    /// This method handles the radar that scans for enemy robots.
    /// </summary>
    private void HandleRadar()
    {
        // Set the radar to turn in the scan direction
        RadarTurnRate = scanDir * 45; // Use a high turn rate
    }
    
    /// <summary>
    /// Method that handles the gun by turning it and firing at a target.
    /// </summary>
    private void HandleGun()
    {
        // Update our target robot to fire at
        UpdateTarget();
        
        // Update the gun direction
        UpdateGunDirection();
        
        // Fires the gun when it's ready
        FireGunWhenReady();
    }
    
    /// <summary>
    /// Method that moves our robot around the battlefield.
    /// </summary>
    private void MoveRobot()
    {
        // The movement strategy is to move as close to our target robot as possible.
        // Our robot should move along the borders all the time, vertically or horizontally.
        // When we get close to our target, or have nowhere to go, our robot should shift its
        // direction from side to side so it doesn't stand still at any time.
        
        int newDirection = direction;
        
        // Get closer to our target if we have a target robot
        if (target != null)
        {
            // Calculate the range from the walls/borders, our robot should keep within
            int borderRange = (int)sentryBorderSize - 20;
            
            // The horizontal and vertical flags are used for determining if our robot should
            // move horizontally or vertically
            bool horizontal = false;
            bool vertical = false;
            
            // Initialize the new heading to the current heading
            double newHeading = Direction;
            
            // Check if our robot is at the upper or lower border and hence should move horizontally
            if (Y < borderRange || Y > ArenaHeight - borderRange)
            {
                horizontal = true;
            }
            
            // Check if our robot is at the left or right border and hence should move vertically
            if (X < borderRange || X > ArenaWidth - borderRange)
            {
                vertical = true;
            }
            
            // If we're in one of the corners of the battlefield, we could move both horizontally
            // or vertically. In this situation, we need to choose one of the two directions.
            if (horizontal && vertical)
            {
                // If the horizontal distance to our target is less than the vertical distance,
                // we choose to move vertically, and hence we clear the horizontal flag.
                if (Math.Abs(target.targetX - X) <= Math.Abs(target.targetY - Y))
                {
                    horizontal = false; // Don't move horizontally => move vertically
                }
            }
            
            // Adjust the heading of our robot with 90 degrees if it must move horizontally.
            // Otherwise the calculated heading is towards moving vertically.
            if (horizontal)
            {
                newHeading -= Math.PI / 2;
            }
            
            // Set the robot to turn the amount of radians we've calculated
            TurnRate = NormalizeRelativeAngle(newHeading - Direction) * (180 / Math.PI);
            
            // Check if our robot has finished turning or has a very low velocity
            if (Math.Abs(TurnRate) < 1 || Math.Abs(Speed) < 0.01)
            {
                // If we should move horizontally, set the robot to move with the
                // horizontal distance to the target robot. Otherwise, use the vertical distance.
                double delta; // delta is the delta distance to move
                if (horizontal)
                {
                    delta = target.targetX - X;
                }
                else
                {
                    delta = target.targetY - Y;
                }
                
                // Set our target speed based on the direction to move
                TargetSpeed = delta > 0 ? 8 : -8;
                
                // Set the new direction of our robot to 1 (meaning move forward) if the delta
                // distance is positive; otherwise it is set to -1 (meaning move backward).
                newDirection = delta > 0 ? 1 : -1;
                
                // Check if more than 10 turns have passed since we changed the direction last time
                if (Time - lastDirectionShift > 10)
                {
                    // Set the new direction to be the reverse direction if the velocity < 1
                    if (Math.Abs(Speed) < 1)
                    {
                        newDirection = direction * -1;
                    }
                    
                    // Check if the direction really changed
                    if (newDirection != direction)
                    {
                        // If the new direction != current direction, set the current direction
                        // to be the new direction and save the current time
                        direction = newDirection;
                        lastDirectionShift = Time;
                    }
                }
            }
        }
        
        // Set speed based on direction
        TargetSpeed = 8 * direction;
    }
    
    /// <summary>
    /// Method that updates the enemy map based on new scan data for a scanned robot.
    /// </summary>
    private void UpdateEnemyMap(ScannedBotEvent e)
    {
        // Get the ID of the scanned robot
        int scannedRobotId = e.ScannedBotId;
        
        // Get robot data for the scanned robot, if we have an entry in the enemy map
        if (!enemyMap.TryGetValue(scannedRobotId, out RobotData scannedRobot))
        {
            // No data entry exists => Create a new data entry for the scanned robot
            scannedRobot = new RobotData(e);
            
            // Put the new data entry into the enemy map
            enemyMap[scannedRobotId] = scannedRobot;
        }
        else
        {
            // Data entry exists => Update the current entry with new scanned data
            scannedRobot.Update(e);
        }
    }
    
    /// <summary>
    /// Method that updates the direction of the radar based on new scan data for a scanned robot.
    /// </summary>
    private void UpdateScanDirection(ScannedBotEvent e)
    {
        // Get the ID of the scanned robot
        int scannedRobotId = e.ScannedBotId;
        
        // Change the scanning direction if and only if we have no record for the oldest scanned
        // robot or the scanned robot IS the oldest scanned robot (based on the ID) AND the enemy
        // map contains scanned data entries for ALL robots
        if ((oldestScanned == null || scannedRobotId == oldestScanned.id) && enemyMap.Count == EnemyCount)
        {
            // Get the oldest scanned robot data
            // In Tank Royale we don't have LinkedHashMap, so we'll use LINQ to get the oldest entry
            RobotData oldestScannedRobot = enemyMap.Values.OrderBy(r => r.lastScannedTime).FirstOrDefault();
            
            if (oldestScannedRobot != null)
            {
                // Get the recent scanned position (x,y) of the oldest scanned robot
                double x = oldestScannedRobot.scannedX;
                double y = oldestScannedRobot.scannedY;
                
                // Calculate the bearing to the oldest scanned robot
                double bearing = BearingTo(x, y);
                
                // Update the scan direction based on the bearing
                // If the bearing is positive, the radar will be moved to the right (clockwise)
                // If the bearing is negative, the radar will be moved to the left (counter-clockwise)
                scanDir = bearing > 0 ? 1 : -1;
                
                // Store this as our oldest scanned
                oldestScanned = oldestScannedRobot;
            }
        }
    }
    
    /// <summary>
    /// Updates the target positions for all enemies using Linear Targeting.
    /// </summary>
    private void UpdateEnemyTargetPositions()
    {
        // Go through all robots in the enemy map
        foreach (RobotData enemy in enemyMap.Values)
        {
            // Variables prefixed with e- refer to enemy and b- refer to bullet
            double bV = 20 - (3 * FIREPOWER); // Bullet speed approximate formula for Tank Royale
            double eX = enemy.scannedX;
            double eY = enemy.scannedY;
            double eV = enemy.scannedVelocity;
            double eH = enemy.scannedHeading * (Math.PI / 180); // Convert to radians
            
            // These constants make calculating the quadratic coefficients below easier
            double A = (eX - X) / bV;
            double B = (eY - Y) / bV;
            double C = eV / bV * Math.Sin(eH);
            double D = eV / bV * Math.Cos(eH);
            
            // Quadratic coefficients: a*(1/t)^2 + b*(1/t) + c = 0
            double a = A * A + B * B;
            double b = 2 * (A * C + B * D);
            double c = (C * C + D * D - 1);
            
            // If the discriminant of the quadratic formula is >= 0, we have a solution
            double discrim = b * b - 4 * a * c;
            if (discrim >= 0)
            {
                // Reciprocal of quadratic formula. Calculate the two possible solutions for time t
                double t1 = 2 * a / (-b - Math.Sqrt(discrim));
                double t2 = 2 * a / (-b + Math.Sqrt(discrim));
                
                // Choose the minimum positive time or select the one closest to 0, if time is negative
                double t = Math.Min(t1, t2) >= 0 ? Math.Min(t1, t2) : Math.Max(t1, t2);
                
                // Calculate the target position (x,y) for the enemy
                double targetX = eX + eV * t * Math.Sin(eH);
                double targetY = eY + eV * t * Math.Cos(eH);
                
                // Limit target position at the walls
                double minX = HALF_BOT_SIZE;
                double minY = HALF_BOT_SIZE;
                double maxX = ArenaWidth - HALF_BOT_SIZE;
                double maxY = ArenaHeight - HALF_BOT_SIZE;
                
                enemy.targetX = Limit(targetX, minX, maxX);
                enemy.targetY = Limit(targetY, minY, maxY);
            }
        }
    }
    
    /// <summary>
    /// Updates which enemy robot from the enemy map should be our current target.
    /// </summary>
    private void UpdateTarget()
    {
        // Set target to null, meaning that we have no target robot yet
        target = null;
        
        // Create a list of possible target robots
        List<RobotData> targets = new List<RobotData>(enemyMap.Values);
        
        // Run through all the possible target robots and remove those outside attack range
        targets.RemoveAll(robot => IsOutsideAttackRange(robot.targetX, robot.targetY));
        
        // Set the target robot to be the closest one to our robot
        double minDist = double.PositiveInfinity;
        foreach (RobotData robot in targets)
        {
            double dist = DistanceTo(robot.targetX, robot.targetY);
            if (dist < minDist)
            {
                minDist = dist;
                target = robot;
            }
        }
        
        // If we still haven't got a target robot, take the first one from our targets list
        if (target == null && targets.Count > 0)
        {
            target = targets[0];
        }
    }
    
    /// <summary>
    /// Method that updates the gun direction to point at the current target.
    /// </summary>
    private void UpdateGunDirection()
    {
        // Only update the gun direction if we have a current target
        if (target != null)
        {
            // Calculate the bearing to the target position
            double targetBearing = BearingTo(target.targetX, target.targetY);
            
            // Convert to gun turn direction and degrees
            double gunTurn = NormalizeRelativeAngle(targetBearing - GunDirection);
            
            // Set the gun turn rate (in degrees per turn)
            GunTurnRate = gunTurn * (180 / Math.PI);
        }
    }
    
    /// <summary>
    /// Method that fires a bullet when the gun is ready to fire.
    /// </summary>
    private void FireGunWhenReady()
    {
        // We only fire the gun when we have a target robot
        if (target != null)
        {
            // Calculate the distance between our robot and the target robot
            double dist = DistanceTo(target.targetX, target.targetY);
            
            // Angle that "covers" the target robot from its center to its edge
            double angle = Math.Atan(HALF_BOT_SIZE / dist);
            
            // Check if the gun is pointing within the angle to hit the target
            if (Math.Abs(GunTurnRate) < angle * (180 / Math.PI))
            {
                // Fire at our target
                Fire(FIREPOWER);
            }
        }
    }
    
    /// <summary>
    /// Method that checks if a coordinate (x,y) is outside the attack range.
    /// </summary>
    private bool IsOutsideAttackRange(double x, double y)
    {
        double minBorderX = sentryBorderSize;
        double minBorderY = sentryBorderSize;
        double maxBorderX = ArenaWidth - sentryBorderSize;
        double maxBorderY = ArenaHeight - sentryBorderSize;
        
        // Is the point inside the "inner" area (outside our patrol area)?
        return (x > minBorderX) && (y > minBorderY) && (x < maxBorderX) && (y < maxBorderY);
    }
    
    /// <summary>
    /// Limits a value to be within a specified range.
    /// </summary>
    private double Limit(double value, double min, double max)
    {
        return Math.Min(max, Math.Max(min, value));
    }
    
    /// <summary>
    /// Normalizes the angle in radians to be between -π and π.
    /// </summary>
    
    /// <summary>
    /// Class for storing data about a robot that has been scanned.
    /// </summary>
    private class RobotData
    {
        public readonly int id;              // ID of the scanned robot
        public double scannedX;              // X coordinate of the scanned robot
        public double scannedY;              // Y coordinate of the scanned robot
        public double scannedVelocity;       // Velocity of the scanned robot
        public double scannedHeading;        // Heading of the scanned robot
        public double targetX;               // Predicted X coordinate to aim at
        public double targetY;               // Predicted Y coordinate to aim at
        public long lastScannedTime;         // Time when the robot was last scanned
        
        /// <summary>
        /// Creates a new robot data entry based on scan data.
        /// </summary>
        public RobotData(ScannedBotEvent e)
        {
            id = e.ScannedBotId;
            Update(e);
            
            // Initialize target coordinates to the scanned position
            targetX = scannedX;
            targetY = scannedY;
        }
        
        /// <summary>
        /// Updates the scanned data based on new scan data.
        /// </summary>
        public void Update(ScannedBotEvent e)
        {
            // In Tank Royale, we can calculate the absolute position based on our bot's position
            double Distance = this.DistanceTo(e.X, e.Y);
            double botX = this.X;
            double botY = this.Y;
            double bearing = this.Direction + e.Direction;
            double distance = Distance;
            
            // Convert bearing to radians
            double bearingRadians = bearing * (Math.PI / 180);
            
            // Calculate position of scanned bot
            scannedX = botX + Math.Sin(bearingRadians) * distance;
            scannedY = botY + Math.Cos(bearingRadians) * distance;
            
            // Store other properties
            scannedVelocity = e.Speed;
            scannedHeading = e.Direction;
            lastScannedTime = Time;
        }
    }
}