using CodinGameFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MeanMax {

	public class Point {
		public double x;
		public double y;

		public Point(double x, double y) {
			this.x = x;
			this.y = y;
		}

		public double Distance(Point p) {
			return Math.Sqrt((this.x - p.x) * (this.x - p.x) + (this.y - p.y) * (this.y - p.y));
		}

		// Move the point to x and y
		public void Move(double x, double y) {
			this.x = x;
			this.y = y;
		}

		// Move the point to an other point for a given distance
		public void MoveTo(Point p, double distance) {
			double d = Distance(p);

			if (d < Referee.EPSILON) {
				return;
			}

			double dx = p.x - x;
			double dy = p.y - y;
			double coef = distance / d;

			this.x += dx * coef;
			this.y += dy * coef;
		}

		public bool IsInRange(Point p, double range) {
			return p != this && Distance(p) <= range;
		}

		override public int GetHashCode() {
			const int prime = 31;
			int result = 1;
			long temp;
			temp = BitConverter.DoubleToInt64Bits(x);
			result = prime * result + (int)(temp ^ (temp >> 32));
			temp = BitConverter.DoubleToInt64Bits(y);
			result = prime * result + (int)(temp ^ (temp >> 32));
			return result;
		}

		override public bool Equals(Object obj) {
			if (this == obj) return true;
			if (obj == null) return false;
			if (GetType() != obj.GetType()) return false;
			Point other = (Point)obj;
			if (BitConverter.DoubleToInt64Bits(x) != BitConverter.DoubleToInt64Bits(other.x)) return false;
			if (BitConverter.DoubleToInt64Bits(y) != BitConverter.DoubleToInt64Bits(other.y)) return false;
			return true;
		}
	}


	public class Wreck : Point {
		internal int id;
		internal double radius;
		internal int water;
		bool known;

		public Wreck(double x, double y, int water, double radius) :
			base(x, y) {

			id = Referee.GLOBAL_ID++;

			this.radius = radius;
			this.water = water;
		}

		String GetFrameId() {
			return id + "@" + water;
		}

		virtual public String ToFrameData() {
			if (known) {
				return GetFrameId().ToString();
			}

			known = true;

			return string.Join("", GetFrameId(), Math.Round(x), Math.Round(y), 0, 0, Referee.TYPE_WRECK, radius);
		}

		// Reaper harvesting
		public bool Harvest(List<Player> players, SortedSet<SkillEffect> skillEffects) {
			players.ForEach(p => {
				if (IsInRange(p.GetReaper(), radius) && !p.GetReaper().isInDoofSkill(skillEffects)) {
					p.score += 1;
					water -= 1;
				}
			});

			return water > 0;
		}
	}


	public abstract class Unit : Point {
		internal int type;
		internal int id;
		internal double vx;
		internal double vy;
		internal double radius;
		internal double mass;
		internal double friction;
		protected bool known;

		public Unit(int type, double x, double y) :
			base(x, y) {

			id = Referee.GLOBAL_ID++;
			this.type = type;

			vx = 0.0;
			vy = 0.0;

			known = false;
		}

		public void Move(double t) {
			x += vx * t;
			y += vy * t;
		}

		public double speed() {
			return Math.Sqrt(vx * vx + vy * vy);
		}

		override
		public int GetHashCode() {
			const int prime = 31;
			int result = 1;
			result = prime * result + id;
			return result;
		}

		override
		public bool Equals(Object obj) {
			if (this == obj)
				return true;
			if (obj == null)
				return false;
			if (GetType() != obj.GetType())
				return false;
			Unit other = (Unit)obj;
			if (id != other.id)
				return false;
			return true;
		}

		String getFrameId() {
			return id.ToString();
		}

		virtual public String toFrameData() {
			if (known) {
				return String.Join(" ", getFrameId(), Math.Round(x), Math.Round(y), Math.Round(vx), Math.Round(vy));
			}

			known = true;

			return string.Join(" ", getFrameId(), Math.Round(x), Math.Round(y), Math.Round(vx), Math.Round(vy), type, Math.Round(radius));
		}

		internal void thrust(Point p, int power) {
			double distance = Distance(p);

			// Avoid a division by zero
			if (Math.Abs(distance) <= Referee.EPSILON) {
				return;
			}

			double coef = (((double)power) / mass) / distance;
			vx += (p.x - this.x) * coef;
			vy += (p.y - this.y) * coef;
		}

		public bool isInDoofSkill(SortedSet<SkillEffect> skillEffects) {
			return skillEffects.Any(s => s is DoofSkillEffect && IsInRange(s, s.radius + radius));
		}

		public void adjust(SortedSet<SkillEffect> skillEffects) {
			x = Referee.Round(x);
			y = Referee.Round(y);

			if (isInDoofSkill(skillEffects)) {
				// No friction if we are in a doof skill effect
				vx = Referee.Round(vx);
				vy = Referee.Round(vy);
			} else {
				vx = Referee.Round(vx * (1.0 - friction));
				vy = Referee.Round(vy * (1.0 - friction));
			}
		}

		// Search the next collision with the map border
		virtual public Collision getCollision() {
			// Check instant collision
			if (Distance(Referee.WATERTOWN) + radius >= Referee.MAP_RADIUS) {
				return new Collision(0.0, this);
			}

			// We are not moving, we can't reach the map border
			if (vx == 0.0 && vy == 0.0) {
				return Referee.NULL_COLLISION;
			}

			// Search collision with map border
			double a = vx * vx + vy * vy;

			if (a <= 0.0) {
				return Referee.NULL_COLLISION;
			}

			double b = 2.0 * (x * vx + y * vy);
			double c = x * x + y * y - (Referee.MAP_RADIUS - radius) * (Referee.MAP_RADIUS - radius);
			double delta = b * b - 4.0 * a * c;

			if (delta <= 0.0) {
				return Referee.NULL_COLLISION;
			}

			double t = (-b + Math.Sqrt(delta)) / (2.0 * a);

			if (t <= 0.0) {
				return Referee.NULL_COLLISION;
			}

			return new Collision(t, this);
		}

		// Search the next collision with an other unit
		public Collision getCollision(Unit u) {
			// Check instant collision
			if (Distance(u) <= radius + u.radius) {
				return new Collision(0.0, this, u);
			}

			// Both units are motionless
			if (vx == 0.0 && vy == 0.0 && u.vx == 0.0 && u.vy == 0.0) {
				return Referee.NULL_COLLISION;
			}

			// Change referencial
			// Unit u is not at point (0, 0) with a speed vector of (0, 0)
			double x2 = x - u.x;
			double y2 = y - u.y;
			double r2 = radius + u.radius;
			double vx2 = vx - u.vx;
			double vy2 = vy - u.vy;

			double a = vx2 * vx2 + vy2 * vy2;

			if (a <= 0.0) {
				return Referee.NULL_COLLISION;
			}

			double b = 2.0 * (x2 * vx2 + y2 * vy2);
			double c = x2 * x2 + y2 * y2 - r2 * r2;
			double delta = b * b - 4.0 * a * c;

			if (delta < 0.0) {
				return Referee.NULL_COLLISION;
			}

			double t = (-b - Math.Sqrt(delta)) / (2.0 * a);

			if (t <= 0.0) {
				return Referee.NULL_COLLISION;
			}

			return new Collision(t, this, u);
		}

		// Bounce between 2 units
		public void bounce(Unit u) {
			double mcoeff = (mass + u.mass) / (mass * u.mass);
			double nx = x - u.x;
			double ny = y - u.y;
			double nxnysquare = nx * nx + ny * ny;
			double dvx = vx - u.vx;
			double dvy = vy - u.vy;
			double product = (nx * dvx + ny * dvy) / (nxnysquare * mcoeff);
			double fx = nx * product;
			double fy = ny * product;
			double m1c = 1.0 / mass;
			double m2c = 1.0 / u.mass;

			vx -= fx * m1c;
			vy -= fy * m1c;
			u.vx += fx * m2c;
			u.vy += fy * m2c;

			fx = fx * Referee.IMPULSE_COEFF;
			fy = fy * Referee.IMPULSE_COEFF;

			// Normalize vector at min or max impulse
			double impulse = Math.Sqrt(fx * fx + fy * fy);
			double coeff = 1.0;
			if (impulse > Referee.EPSILON && impulse < Referee.MIN_IMPULSE) {
				coeff = Referee.MIN_IMPULSE / impulse;
			}

			fx = fx * coeff;
			fy = fy * coeff;

			vx -= fx * m1c;
			vy -= fy * m1c;
			u.vx += fx * m2c;
			u.vy += fy * m2c;

			double diff = (Distance(u) - radius - u.radius) / 2.0;
			if (diff <= 0.0) {
				// Unit overlapping. Fix positions.
				MoveTo(u, diff - Referee.EPSILON);
				u.MoveTo(this, diff - Referee.EPSILON);
			}
		}

		// Bounce with the map border
		public void bounce() {
			double mcoeff = 1.0 / mass;
			double nxnysquare = x * x + y * y;
			double product = (x * vx + y * vy) / (nxnysquare * mcoeff);
			double fx = x * product;
			double fy = y * product;

			vx -= fx * mcoeff;
			vy -= fy * mcoeff;

			fx = fx * Referee.IMPULSE_COEFF;
			fy = fy * Referee.IMPULSE_COEFF;

			// Normalize vector at min or max impulse
			double impulse = Math.Sqrt(fx * fx + fy * fy);
			double coeff = 1.0;
			if (impulse > Referee.EPSILON && impulse < Referee.MIN_IMPULSE) {
				coeff = Referee.MIN_IMPULSE / impulse;
			}

			fx = fx * coeff;
			fy = fy * coeff;
			vx -= fx * mcoeff;
			vy -= fy * mcoeff;

			double diff = Distance(Referee.WATERTOWN) + radius - Referee.MAP_RADIUS;
			if (diff >= 0.0) {
				// Unit still outside of the map, reposition it
				MoveTo(Referee.WATERTOWN, diff + Referee.EPSILON);
			}
		}

		virtual public int getExtraInput() {
			return -1;
		}

		virtual public int getExtraInput2() {
			return -1;
		}

		virtual public int getPlayerIndex() {
			return -1;
		}
	}

	public class Tanker : Unit {
		public int water;
		public int size;
		public Player player;
		public  bool killed;

		public Tanker(int size, Player player) :
			base(Referee.TYPE_TANKER, 0.0, 0.0) {
			this.player = player;
			this.size = size;

			water = Referee.TANKER_EMPTY_WATER;
			mass = Referee.TANKER_EMPTY_MASS + Referee.TANKER_MASS_BY_WATER * water;
			friction = Referee.TANKER_FRICTION;
			radius = Referee.TANKER_RADIUS_BASE + Referee.TANKER_RADIUS_BY_SIZE * size;
		}

		String getFrameId() {
			return id + "@" + water;
		}

		internal Wreck die() {
			// Don't spawn a wreck if our center is outside of the map
			if (Distance(Referee.WATERTOWN) >= Referee.MAP_RADIUS) {
				return null;
			}

			return new Wreck(Referee.Round(x), Referee.Round(y), water, radius);
		}

		internal bool isFull() {
			return water >= size;
		}

		public void play() {
			if (isFull()) {
				// Try to leave the map
				thrust(Referee.WATERTOWN, -Referee.TANKER_THRUST);
			} else if (Distance(Referee.WATERTOWN) > Referee.WATERTOWN_RADIUS) {
				// Try to reach watertown
				thrust(Referee.WATERTOWN, Referee.TANKER_THRUST);
			}
		}

		override public Collision getCollision() {
			// Tankers can go outside of the map
			return Referee.NULL_COLLISION;
		}

		override public int getExtraInput() {
			return water;
		}

		override public int getExtraInput2() {
			return size;
		}
	}

	public class NoRageException : Exception { }
	public class TooFarException : Exception { }

	public abstract class Looter : Unit {
		internal int skillCost;
		internal double skillRange;
		internal bool skillActive;

		internal Player player;

		internal Point wantedThrustTarget;
		internal int wantedThrustPower;

		internal String message;
		internal Referee.Action attempt;
		internal SkillResult skillResult;

		public Looter(int type, Player player, double x, double y) :
			base(type, x, y) {

			this.player = player;

			radius = Referee.LOOTER_RADIUS;
		}

		public SkillEffect skill(Point p) {
			if (player.rage < skillCost)
				throw new NoRageException();
			if (Distance(p) > skillRange)
				throw new TooFarException();

			player.rage -= skillCost;
			return SkillImpl(p);
		}

		override public string toFrameData() {
			if (known) {
				return base.toFrameData();
			}

			return string.Join(" ", base.toFrameData(), player.index);
		}

		override public int getPlayerIndex() {
			return player.index;
		}

		public abstract SkillEffect SkillImpl(Point p);

		public void setWantedThrust(Point target, int power) {
			if (power < 0) {
				power = 0;
			}

			wantedThrustTarget = target;
			wantedThrustPower = Math.Min(power, Referee.MAX_THRUST);
		}

		public void reset() {
			message = null;
			attempt = 0;
			skillResult = null;
			wantedThrustTarget = null;
		}
	}

	public class Reaper : Looter {
		public Reaper(Player player, double x, double y) :
			base(Referee.LOOTER_REAPER, player, x, y) {

			mass = Referee.REAPER_MASS;
			friction = Referee.REAPER_FRICTION;
			skillCost = Referee.REAPER_SKILL_COST;
			skillRange = Referee.REAPER_SKILL_RANGE;
			skillActive = Referee.REAPER_SKILL_ACTIVE;
		}

		public override SkillEffect SkillImpl(Point p) {
			return new ReaperSkillEffect(Referee.TYPE_REAPER_SKILL_EFFECT, p.x, p.y, Referee.REAPER_SKILL_RADIUS, Referee.REAPER_SKILL_DURATION, Referee.REAPER_SKILL_ORDER, this);
		}
	}

	public class Destroyer : Looter {
		public Destroyer(Player player, double x, double y) :
			base(Referee.LOOTER_DESTROYER, player, x, y) {

			mass = Referee.DESTROYER_MASS;
			friction = Referee.DESTROYER_FRICTION;
			skillCost = Referee.DESTROYER_SKILL_COST;
			skillRange = Referee.DESTROYER_SKILL_RANGE;
			skillActive = Referee.DESTROYER_SKILL_ACTIVE;
		}

		public override SkillEffect SkillImpl(Point p) {
			return new DestroyerSkillEffect(Referee.TYPE_DESTROYER_SKILL_EFFECT, p.x, p.y, Referee.DESTROYER_SKILL_RADIUS, Referee.DESTROYER_SKILL_DURATION,
					Referee.DESTROYER_SKILL_ORDER, this);
		}
	}

	public class Doof : Looter {
		public Doof(Player player, double x, double y) :
			base(Referee.LOOTER_DOOF, player, x, y) {

			mass = Referee.DOOF_MASS;
			friction = Referee.DOOF_FRICTION;
			skillCost = Referee.DOOF_SKILL_COST;
			skillRange = Referee.DOOF_SKILL_RANGE;
			skillActive = Referee.DOOF_SKILL_ACTIVE;
		}

		override public SkillEffect SkillImpl(Point p) {
			return new DoofSkillEffect(Referee.TYPE_DOOF_SKILL_EFFECT, p.x, p.y, Referee.DOOF_SKILL_RADIUS, Referee.DOOF_SKILL_DURATION, Referee.DOOF_SKILL_ORDER, this);
		}

		// With flame effects! Yeah!
		public int Sing() {
			return (int)Math.Floor(speed() * Referee.DOOF_RAGE_COEF);
		}
	}

	public class Player {
		internal int score;
		internal int index;
		internal int rage;
		internal Looter[] looters;
		internal bool dead;
		internal Queue<TankerSpawn> tankers;

		public Player(int index) {
			this.index = index;

			looters = new Looter[Referee.LOOTER_COUNT];
		}

		public void Kill() {
			dead = true;
		}

		internal Reaper GetReaper() {
			return (Reaper)looters[Referee.LOOTER_REAPER];
		}

		internal Destroyer GetDestroyer() {
			return (Destroyer)looters[Referee.LOOTER_DESTROYER];
		}

		internal Doof GetDoof() {
			return (Doof)looters[Referee.LOOTER_DOOF];
		}
	}

	public class TankerSpawn {
		internal int size;
		internal double angle;

		public TankerSpawn(int size, double angle) {
			this.size = size;
			this.angle = angle;
		}
	}

	public class Collision {
		internal double t;
		internal Unit a;
		internal Unit b;

		public Collision(double t) :
			this(t, null, null) {
		}

		public Collision(double t, Unit a) :
			this(t, a, null) {
		}

		public Collision(double t, Unit a, Unit b) {
			this.t = t;
			this.a = a;
			this.b = b;
		}

		internal Tanker Dead() {
			if (a is Destroyer && b is Tanker && b.mass < Referee.REAPER_SKILL_MASS_BONUS) {
				return (Tanker)b;
			}

			if (b is Destroyer && a is Tanker && a.mass < Referee.REAPER_SKILL_MASS_BONUS) {
				return (Tanker)a;
			}

			return null;
		}
	}

	public abstract class SkillEffect : Point {
		internal int id;
		internal int type;
		internal double radius;
		internal int duration;
		internal int order;
		bool known;
		Looter looter;

		public SkillEffect(int type, double x, double y, double radius, int duration, int order, Looter looter) :
		base(x, y) {
			id = Referee.GLOBAL_ID++;

			this.type = type;
			this.radius = radius;
			this.duration = duration;
			this.looter = looter;
			this.order = order;
		}

		internal void Apply(List<Unit> units) {
			duration -= 1;
			ApplyImpl(units.Where(u => IsInRange(u, radius + u.radius)).ToList());
		}

		virtual public String ToFrameData() {
			if (known) {
				return id.ToString();
			}

			known = true;

			return String.Join("", id, Math.Round(x), Math.Round(y), looter.id, 0, type, Math.Round(radius));
		}


		public abstract void ApplyImpl(List<Unit> units);


		override public int GetHashCode() {
			const int prime = 31;
			int result = 1;
			result = prime * result + id;
			return result;
		}

		override public bool Equals(Object obj) {
			if (this == obj) return true;
			if (obj == null) return false;
			if (GetType() != obj.GetType()) return false;
			SkillEffect other = (SkillEffect)obj;
			if (id != other.id) return false;
			return true;
		}
	}

	public class ReaperSkillEffect : SkillEffect {

		public ReaperSkillEffect(int type, double x, double y, double radius, int duration, int order, Reaper reaper) :
			base(type, x, y, radius, duration, order, reaper) {
		}

		override public void ApplyImpl(List<Unit> units) {
			// Increase mass
			units.ForEach(u => u.mass += Referee.REAPER_SKILL_MASS_BONUS);
		}
	}

	public class DestroyerSkillEffect : SkillEffect {

		public DestroyerSkillEffect(int type, double x, double y, double radius, int duration, int order, Destroyer destroyer) :
			base(type, x, y, radius, duration, order, destroyer) {
		}

		override public void ApplyImpl(List<Unit> units) {
			// Push units
			units.ForEach(u => u.thrust(this, -Referee.DESTROYER_NITRO_GRENADE_POWER));
		}
	}

	public class DoofSkillEffect : SkillEffect {

		public DoofSkillEffect(int type, double x, double y, double radius, int duration, int order, Doof doof) :
			base(type, x, y, radius, duration, order, doof) {


		}

		override public void ApplyImpl(List<Unit> units) {
			// Nothing to do now
		}
	}

	public class SkillEffectComparer : IComparer<SkillEffect> {

		public int Compare(SkillEffect a, SkillEffect b) {
			int order = a.order - b.order;

			if (order != 0) {
				return order;
			}

			return a.id - b.id;
		}
	}

	internal class SkillResult {
		public const int OK = 0;
		public const int NO_RAGE = 1;
		public const int TOO_FAR = 2;
		Point target;
		internal int code;

		internal SkillResult(int x, int y) {
			target = new Point(x, y);
			code = OK;
		}

		int getX() {
			return (int)target.x;
		}

		int getY() {
			return (int)target.y;
		}
	}
	public class Referee {
		private const int GAME_VERSION = 3;

		public const bool SPAWN_WRECK = false;
		public const int LOOTER_COUNT = 3;
		public const bool REAPER_SKILL_ACTIVE = true;
		public const bool DESTROYER_SKILL_ACTIVE = true;
		public const bool DOOF_SKILL_ACTIVE = true;
		public const string EXPECTED = "<x> <y> <power> | SKILL <x> <y> | WAIT";


		public const double MAP_RADIUS = 6000.0;
		public static int TANKERS_BY_PLAYER;
		public const int TANKERS_BY_PLAYER_MIN = 1;
		public const int TANKERS_BY_PLAYER_MAX = 3;

		public const double WATERTOWN_RADIUS = 3000.0;

		public const int TANKER_THRUST = 500;
		public const double TANKER_EMPTY_MASS = 2.5;
		public const double TANKER_MASS_BY_WATER = 0.5;
		public const double TANKER_FRICTION = 0.40;
		public const double TANKER_RADIUS_BASE = 400.0;
		public const double TANKER_RADIUS_BY_SIZE = 50.0;
		public const int TANKER_EMPTY_WATER = 1;
		public const int TANKER_MIN_SIZE = 4;
		public const int TANKER_MAX_SIZE = 10;
		public const double TANKER_MIN_RADIUS = TANKER_RADIUS_BASE + TANKER_RADIUS_BY_SIZE * TANKER_MIN_SIZE;
		public const double TANKER_MAX_RADIUS = TANKER_RADIUS_BASE + TANKER_RADIUS_BY_SIZE * TANKER_MAX_SIZE;
		public const double TANKER_SPAWN_RADIUS = 8000.0;
		public const int TANKER_START_THRUST = 2000;

		public const int MAX_THRUST = 300;
		public const int MAX_RAGE = 300;
		public const int WIN_SCORE = 50;

		public const double REAPER_MASS = 0.5;
		public const double REAPER_FRICTION = 0.20;
		public const int REAPER_SKILL_DURATION = 3;
		public const int REAPER_SKILL_COST = 30;
		public const int REAPER_SKILL_ORDER = 0;
		public const double REAPER_SKILL_RANGE = 2000.0;
		public const double REAPER_SKILL_RADIUS = 1000.0;
		public const double REAPER_SKILL_MASS_BONUS = 10.0;

		public const double DESTROYER_MASS = 1.5;
		public const double DESTROYER_FRICTION = 0.30;
		public const int DESTROYER_SKILL_DURATION = 1;
		public const int DESTROYER_SKILL_COST = 60;
		public const int DESTROYER_SKILL_ORDER = 2;
		public const double DESTROYER_SKILL_RANGE = 2000.0;
		public const double DESTROYER_SKILL_RADIUS = 1000.0;
		public const int DESTROYER_NITRO_GRENADE_POWER = 1000;

		public const double DOOF_MASS = 1.0;
		public const double DOOF_FRICTION = 0.25;
		public const double DOOF_RAGE_COEF = 1.0 / 100.0;
		public const int DOOF_SKILL_DURATION = 3;
		public const int DOOF_SKILL_COST = 30;
		public const int DOOF_SKILL_ORDER = 1;
		public const double DOOF_SKILL_RANGE = 2000.0;
		public const double DOOF_SKILL_RADIUS = 1000.0;

		public const double LOOTER_RADIUS = 400.0;
		public const int LOOTER_REAPER = 0;
		public const int LOOTER_DESTROYER = 1;
		public const int LOOTER_DOOF = 2;

		public const int TYPE_TANKER = 3;
		public const int TYPE_WRECK = 4;
		public const int TYPE_REAPER_SKILL_EFFECT = 5;
		public const int TYPE_DOOF_SKILL_EFFECT = 6;
		public const int TYPE_DESTROYER_SKILL_EFFECT = 7;

		public const double EPSILON = 0.00001;
		public const double MIN_IMPULSE = 30.0;
		public const double IMPULSE_COEFF = 0.5;

		// Global first free id for all elements on the map 
		public static int GLOBAL_ID = 0;

        public Referee()
        {
            GLOBAL_ID = 0;
        }

		// Center of the map
		public static readonly Point WATERTOWN = new Point(0, 0);

		// The null collision 
		public static readonly Collision NULL_COLLISION = new Collision(1.0 + EPSILON);




		static public int Round(double x) {
			int s = x < 0 ? -1 : 1;
			return s * (int)Math.Round(s * x);
		}

		public int seed;
		int playerCount;
		List<Unit> units;
		List<Looter> looters;
		List<Tanker> tankers;
		List<Tanker> deadTankers;
		List<Wreck> wrecks;
		List<Player> players;
		List<String> frameData;
		SortedSet<SkillEffect> skillEffects;

		void SpawnTanker(Player player) {
			TankerSpawn spawn = player.tankers.Dequeue();

			double angle = (player.index + spawn.angle) * Math.PI * 2.0 / ((double)playerCount);

			double cos = Math.Cos(angle);
			double sin = Math.Sin(angle);

			Tanker tanker = new Tanker(spawn.size, player);

			double distance = TANKER_SPAWN_RADIUS + tanker.radius;

			bool safe = false;
			while (!safe) {
				tanker.Move(cos * distance, sin * distance);
				safe = units.TrueForAll(u => tanker.Distance(u) > tanker.radius + u.radius);
				distance += TANKER_MIN_RADIUS;
			}

			tanker.thrust(WATERTOWN, TANKER_START_THRUST);

			units.Add(tanker);
			tankers.Add(tanker);
		}

		Looter createLooter(int type, Player player, double x, double y) {
			if (type == LOOTER_REAPER) {
				return new Reaper(player, x, y);
			} else if (type == LOOTER_DESTROYER) {
				return new Destroyer(player, x, y);
			} else if (type == LOOTER_DOOF) {
				return new Doof(player, x, y);
			}

			// Not supposed to happen
			return null;
		}

		void newFrame(double t) {
			frameData.Add("#" + String.Format("%.5f", t));
		}

		void addToFrame(Wreck w) {
			frameData.Add(w.ToFrameData());
		}

		void addToFrame(Unit u) {
			frameData.Add(u.toFrameData());
		}

		void addToFrame(SkillEffect s) {
			frameData.Add(s.ToFrameData());
		}

		void addDeadToFrame(SkillEffect s) {
			frameData.Add(String.Join(" ",s.ToFrameData(), "d"));
		}

		void addDeadToFrame(Unit u) {
			frameData.Add(String.Join(" ", u.toFrameData(), "d"));
		}

		void addDeadToFrame(Wreck w) {
			frameData.Add(String.Join(" ", w.ToFrameData(), "d"));
		}

		void snapshot() {
			frameData.AddRange(looters.Select<Looter,string>(u => u.toFrameData()));
			frameData.AddRange(tankers.Select(u => u.toFrameData()));
			frameData.AddRange(wrecks.Select(w => w.ToFrameData()));
			frameData.AddRange(skillEffects.Select(s => s.ToFrameData()));
		}


		public void initReferee(int playerCount, Properties prop) {
			this.playerCount = playerCount;

			if (int.TryParse(prop.getProperty("seed", DateTime.Now.Millisecond.ToString()), out this.seed) == false) {
				this.seed = DateTime.Now.Millisecond;
			}

			Random random = new Random(this.seed);


			TANKERS_BY_PLAYER = TANKERS_BY_PLAYER_MIN + random.Next(TANKERS_BY_PLAYER_MAX - TANKERS_BY_PLAYER_MIN + 1);

			units = new List<Unit>();
			looters = new List<Looter>();
			tankers = new List<Tanker>();
			deadTankers = new List<Tanker>();
			wrecks = new List<Wreck>();
			players = new List<Player>();


			frameData = new List<string>();

			skillEffects = new SortedSet<SkillEffect>(new SkillEffectComparer());

			// Create players
			for (int i = 0; i < playerCount; ++i) {
				Player player = new Player(i);
				players.Add(player);
			}

			// Generate the map
			Queue<TankerSpawn> queue = new Queue<TankerSpawn>();
			for (int i = 0; i < 500; ++i) {
				queue.Enqueue(new TankerSpawn(TANKER_MIN_SIZE + random.Next(TANKER_MAX_SIZE - TANKER_MIN_SIZE),
						random.NextDouble()));
			}
			players.ForEach(p => p.tankers = queue);

			// Create looters
			foreach (Player player in players) {
				for (int i = 0; i < LOOTER_COUNT; ++i) {
					Looter looter = createLooter(i, player, 0, 0);
					player.looters[i] = looter;
					units.Add(looter);
					looters.Add(looter);
				}
			}

			// Random spawns for looters
			bool finished = false;
			while (!finished) {
				finished = true;

				for (int i = 0; i < LOOTER_COUNT && finished; ++i) {
					double distance = random.NextDouble() * (MAP_RADIUS - LOOTER_RADIUS);
					double angle = random.NextDouble();

					foreach (Player player in players) {
						double looterAngle = (player.index + angle) * (Math.PI * 2.0 / ((double)playerCount));
						double cos = Math.Cos(looterAngle);
						double sin = Math.Sin(looterAngle);

						Looter looter = player.looters[i];
						looter.Move(cos * distance, sin * distance);

						// If the looter touch a looter, reset everyone and try again
						if (units.Any(u => u != looter && looter.Distance(u) <= looter.radius + u.radius)) {
							finished = false;
							looters.ForEach(l => l.Move(0.0, 0.0));
							break;
						}
					}
				}
			}

			// Spawn start tankers
			for (int j = 0; j < Referee.TANKERS_BY_PLAYER; ++j) {
				foreach (Player player in players) {
					SpawnTanker(player);
				}
			}

			adjust();
			newFrame(1.0);
			snapshot();
		}


		protected Properties getConfiguration() {
			Properties properties = new Properties();

			properties.Add("seed", seed.ToString());

			return properties;
		}

		
		public String[] getInitInputForPlayer(int playerIdx) {
			List<String> lines = new List<String>();

			// No init input

			return lines.ToArray();
		}

		
		public void prepare(int round) {
			frameData.Clear();
			looters.ForEach(l => l.reset());
		}

		int getPlayerId(int id, int forId) {
			// This method can be called with id=-1 because of the default player for units
			if (id < 0) {
				return id;
			}

			if (id == forId) {
				return 0;
			}

			if (id < forId) {
				return id + 1;
			}

			return id;
		}


		public String[] getInputForPlayer(int round, int playerIdx) {
			List<String> lines = new List<String>();

			// Scores
			// My score is always first
			lines.Add(players[playerIdx].score.ToString());
			for (int i = 0; i < playerCount; ++i) {
				if (i != playerIdx) {
					lines.Add(players[i].score.ToString());
				}
			}

			// Rages
			// My rage is always first
			lines.Add(players[playerIdx].rage.ToString());
			for (int i = 0; i < playerCount; ++i) {
				if (i != playerIdx) {
					lines.Add(players[i].rage.ToString());
				}
			}

			// Units
			List<String> unitsLines = new List<String>();
			// Looters and tankers
			unitsLines.AddRange(units.Select(
					u => String.Join(" ", u.id, u.type, getPlayerId(u.getPlayerIndex(), playerIdx), u.mass, Round(u.radius), Round(u.x), Round(u.y), Round(u.vx), Round(u.vy),u.getExtraInput(), u.getExtraInput2()))
					);
			// Wrecks
			unitsLines.AddRange(wrecks.Select(w => String.Join(" ", w.id, TYPE_WRECK, -1, -1, Round(w.radius), Round(w.x), Round(w.y), 0, 0, w.water, -1)));
			// Skill effects
			unitsLines.AddRange(skillEffects.Select(s => String.Join(" ", s.id, s.type, -1, -1, Round(s.radius), Round(s.x), Round(s.y), 0, 0, s.duration, -1)));

			lines.Add(unitsLines.Count.ToString());
			lines.AddRange(unitsLines);

			return lines.ToArray();
		}

		
		public int getExpectedOutputLineCountForPlayer(int playerIdx) {
			return 3;
		}

		static readonly Regex PLAYER_MOVE_PATTERN = new Regex
				("^(?<x>-?[0-9]{1,9})\\s+(?<y>-?[0-9]{1,9})\\s+(?<power>([0-9]{1,9}))?(?:\\s+(?<message>.+))?$");
		static readonly Regex PLAYER_SKILL_PATTERN = new Regex("^SKILL\\s+(?<x>-?[0-9]{1,9})\\s+(?<y>-?[0-9]{1,9})(?:\\s+(?<message>.+))?$",
				RegexOptions.IgnoreCase);
		static readonly Regex PLAYER_WAIT_PATTERN = new Regex("^WAIT(?:\\s+(?<message>.+))?$", RegexOptions.IgnoreCase);


		public enum Action {
			SKILL, MOVE, WAIT
		}

		
		public void handlePlayerOutput(int frame, int round, int playerIdx, String[] outputs) {

			Player player = players[playerIdx];
			String expected = EXPECTED;

			for (int i = 0; i < LOOTER_COUNT; ++i) {
				String line = outputs[i];
				Match match;
				try {
					Looter looter = players[playerIdx].looters[i];

					match = PLAYER_WAIT_PATTERN.Match(line);
					if (match.Success) {
						looter.attempt = Action.WAIT;
						matchMessage(looter, match);
						continue;
					}

					match = PLAYER_MOVE_PATTERN.Match(line);
					if (match.Success) {
						looter.attempt = Action.MOVE;
						int x = Int32.Parse(match.Groups["x"].Value);
						int y = Int32.Parse(match.Groups["y"].Value);
						int power = Int32.Parse(match.Groups["power"].Value);

						looter.setWantedThrust(new Point(x, y), power);
						matchMessage(looter, match);
						continue;
					}

					match = PLAYER_SKILL_PATTERN.Match(line);
					if (match.Success) {
						if (!looter.skillActive) {
							// Don't kill the player for that. Just do a WAIT instead
							looter.attempt = Action.WAIT;
							matchMessage(looter, match);
							continue;
						}

						looter.attempt = Action.SKILL;
						int x = Int32.Parse(match.Groups["x"].Value);
						int y = Int32.Parse(match.Groups["y"].Value);

						SkillResult result = new SkillResult(x, y);
						looter.skillResult = result;

						try {
							SkillEffect effect = looter.skill(new Point(x, y));
							skillEffects.Add(effect);
						} catch (NoRageException /*e*/) {
							result.code = SkillResult.NO_RAGE;
						} catch (TooFarException /*e*/) {
							result.code = SkillResult.TOO_FAR;
						}
						matchMessage(looter, match);
						continue;
					}

					throw new InvalidInputException(expected, line);
				} catch (InvalidInputException e) {
					player.Kill();
					throw e;
				} catch (Exception e) {
                    //StringWriter errors = new StringWriter();
                    //e.printStackTrace(new PrintWriter(errors));
                    //printError(e.getMessage() + "\n" + errors.toString());
                    System.Diagnostics.Debug.WriteLine(e);
					player.Kill();
					throw new InvalidInputException(expected, line);
				}
			}
		}

		
		protected int getMillisTimeForRound() {
			return 50;
		}

		private void matchMessage(Looter looter, Match match) {
			looter.message = match.Groups["message"].Value;
			if (looter.message != null && looter.message.Length > 19) {
				looter.message = looter.message.Substring(0, 17) + "...";
			}
		}

		// Get the next collision for the current round
		// All units are tested
		Collision getNextCollision() {
			Collision result = NULL_COLLISION;

			for (int i = 0; i < units.Count; ++i) {
				Unit unit = units[i];

				// Test collision with map border first
				Collision collision = unit.getCollision();

				if (collision.t < result.t) {
					result = collision;
				}

				for (int j = i + 1; j < units.Count; ++j) {
					collision = unit.getCollision(units[j]);

					if (collision.t < result.t) {
						result = collision;
					}
				}
			}

			return result;
		}

		// Play a collision
		void playCollision(Collision collision) {
			if (collision.b == null) {
				// Bounce with border
				addToFrame(collision.a);
				collision.a.bounce();
			} else {
				Tanker dead = collision.Dead();

				if (dead != null) {
					// A destroyer kill a tanker
					addDeadToFrame(dead);
					deadTankers.Add(dead);
					tankers.Remove(dead);
					units.Remove(dead);

					Wreck wreck = dead.die();

					// If a tanker is too far away, there's no wreck
					if (wreck != null) {
						wrecks.Add(wreck);
						addToFrame(wreck);
					}
				} else {
					// Bounce between two units
					addToFrame(collision.a);
					addToFrame(collision.b);
					collision.a.bounce(collision.b);
				}
			}
		}


		public void updateGame(int round) {
			// Apply skill effects
			foreach(SkillEffect effect in skillEffects) {
				effect.Apply(units);
			}

			// Apply thrust for tankers
			foreach (Tanker tt in tankers) {
				tt.play();
			}

			// Apply wanted thrust for looters
			foreach (Player player in players) {
				foreach (Looter looter in player.looters) {
					if (looter.wantedThrustTarget != null) {
						looter.thrust(looter.wantedThrustTarget, looter.wantedThrustPower);
					}
				}
			}

			double t = 0.0;

			// Play the round. Stop at each collisions and play it. Reapeat until t > 1.0

			Collision collision = getNextCollision();

			while (collision.t + t <= 1.0) {
				double deltab = collision.t;
				units.ForEach(u => u.Move(deltab));
				t += collision.t;

				newFrame(t);

				playCollision(collision);

				collision = getNextCollision();
			}

			// No more collision. Move units until the end of the round
			double delta = 1.0 - t;
			units.ForEach(u => u.Move(delta));

			List<Tanker> tankersToRemove = new List<Tanker>();

			tankers.ForEach(tanker => {
				double distance = tanker.Distance(WATERTOWN);
				bool full = tanker.isFull();

				if (distance <= WATERTOWN_RADIUS && !full) {
					// A non full tanker in watertown collect some water
					tanker.water += 1;
					tanker.mass += TANKER_MASS_BY_WATER;
				} else if (distance >= TANKER_SPAWN_RADIUS + tanker.radius && full) {
					// Remove too far away and not full tankers from the game
					tankersToRemove.Add(tanker);
				}
			});

			newFrame(1.0);
			snapshot();

			if (tankersToRemove.Count() > 0) {
				tankersToRemove.ForEach(tanker => addDeadToFrame(tanker));
			}

			units.RemoveAll(u=> tankersToRemove.Contains(u));
			tankers.RemoveAll(u => tankersToRemove.Contains(u));
			deadTankers.AddRange(tankersToRemove);

			// Spawn new tankers for each dead tanker during the round
			deadTankers.ForEach(tanker => SpawnTanker(tanker.player));
			deadTankers.Clear();

			HashSet<Wreck> deadWrecks = new HashSet<Wreck>();

			// Water collection for reapers
			wrecks = wrecks.FindAll(w => {
				bool alive = w.Harvest(players, skillEffects);

				if (!alive) {
					addDeadToFrame(w);
					deadWrecks.Add(w);
				}

				return alive;
			}).ToList();


			// Round values and apply friction
			adjust();

			// Generate rage
			if (LOOTER_COUNT >= 3) {
				players.ForEach(p => p.rage = Math.Min(MAX_RAGE, p.rage + p.GetDoof().Sing()));
			}

			// Restore masses
			units.ForEach(u => {
				while (u.mass >= REAPER_SKILL_MASS_BONUS) {
					u.mass -= REAPER_SKILL_MASS_BONUS;
				}
			});

			// Remove dead skill effects
			HashSet<SkillEffect> effectsToRemove = new HashSet<SkillEffect>();
			foreach (SkillEffect effect in skillEffects) {
				if (effect.duration <= 0) {
					addDeadToFrame(effect);
					effectsToRemove.Add(effect);
				}
			}
			skillEffects.RemoveWhere(s => effectsToRemove.Contains(s));
		}

		protected void adjust() {
			units.ForEach(u => u.adjust(skillEffects));
		}


		public void populateMessages(Properties p) {
			//TODO: write some text
			p.Add("Move", "$%d moved looter %d towards (%d,%d) with power %d");
			p.Add("SkillFailedTooFar", "$%d %d %d %d");
			p.Add("SkillFailedNoRage", "$%d %d %d %d");
			p.Add("SkillDestroyer", "$%d %d %d %d");
			p.Add("SkillRepear", "$%d %d %d %d");
			p.Add("SkillDoof", "$%d %d %d %d");
		}


		public bool isGameOver() {
			if (players.Any(p => p.score >= WIN_SCORE)) {
				// We got a winner !
				return true;
			}

			var alive = players.Where(p => !p.dead);

			if (alive.Count() == 1) { 
				Player survivor = alive.First();

				// If only one player is alive and he got the highest score, end the game now.
				return players.Where(p => p != survivor).All(p => p.score < survivor.score);
			}

			// Everyone is dead. End of the game.
			return alive.Count() == 0;
		}


		public String[] getInitDataForView() {
			List<String> lines = new List<String>();

			lines.Add(playerCount.ToString());
			lines.Add(Math.Round(MAP_RADIUS).ToString());
			lines.Add(Math.Round(WATERTOWN_RADIUS).ToString());
			lines.Add(LOOTER_COUNT.ToString());

			return lines.ToArray();
		}


		//protected String[] getFrameDataForView(int round, int frame, bool keyFrame) {
		//	List<String> lines = new List<string>();

		//	lines.AddRange(players.stream().map(p => String.valueOf(p.score)).collect(Collectors.toList()));
		//	lines.AddRange(players.stream().map(p => String.valueOf(p.rage)).collect(Collectors.toList()));
		//	lines.AddRange(looters.stream().map(l => String.valueOf(l.message == null ? "" : l.message)).collect(Collectors.toList()));
		//	lines.AddRange(frameData);

		//	return lines.ToArray();
		//}




		public int getMinimumPlayerCount() {
			return 3;
		}


		public String[] getPlayerActions(int playerIdx, int round) {
			return new String[0];
		}


		public bool isPlayerDead(int playerIdx) {
			return players[playerIdx].dead;
		}


		public String getDeathReason(int playerIdx) {
			return "$" + playerIdx + ": Eliminated!";
		}


		public int getScore(int playerIdx) {
			return players[playerIdx].score;
		}



		public void setPlayerTimeout(int frame, int round, int playerIdx) {
			players[playerIdx].Kill();
		}


		public int getMaxRoundCount(int playerCount) {
			return 200;
		}



	}



}
