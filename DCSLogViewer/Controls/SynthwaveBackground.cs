using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DCSLogViewer.Controls;

/// <summary>
/// Animated synthwave/retrowave background with perspective grid,
/// gradient sky, sun, mountains, palm trees, and military aircraft combat.
/// </summary>
public class SynthwaveBackground : Control
{
    private double _time;
    private readonly DispatcherTimer _timer;
    private readonly List<Aircraft> _aircraft = new();
    private readonly List<Tracer> _tracers = new();
    private readonly List<Missile> _missiles = new();
    private readonly List<IncomingMissile> _incomingMissiles = new();
    private readonly List<Explosion> _explosions = new();
    private readonly List<SmokeParticle> _smoke = new();
    private readonly Random _rng = new();

    // SAM site state
    private readonly SamSite _samLeft = new();
    private readonly SamSite _samRight = new();
    private bool _samsInitialized;

    public SynthwaveBackground()
    {
        ClipToBounds = true;
        IsHitTestVisible = false;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30fps
        };
        _timer.Tick += (_, _) =>
        {
            _time += 0.033;
            UpdateSamSites();
            UpdateAircraft();
            UpdateTracers();
            UpdateMissiles();
            UpdateIncomingMissiles();
            UpdateExplosions();
            UpdateSmoke();
            InvalidateVisual();
        };

        Loaded += (_, _) =>
        {
            InitAircraft();
            _timer.Start();
        };
        Unloaded += (_, _) => _timer.Stop();
    }

    // ───────────────────────────────────────────────────────────────
    //  AIRCRAFT DEFINITIONS
    // ───────────────────────────────────────────────────────────────

    private void InitAircraft()
    {
        for (int i = 0; i < 3; i++)
            SpawnAircraft(randomStart: true);
    }

    private static readonly AircraftType[] JetTypes = { AircraftType.F16, AircraftType.FA18, AircraftType.F15, AircraftType.A10, AircraftType.F14 };
    private static readonly AircraftType[] HeliTypes = { AircraftType.AH64, AircraftType.Mi28, AircraftType.Hind, AircraftType.UH60, AircraftType.UH1 };

    private static bool IsHelicopter(AircraftType t) =>
        t == AircraftType.AH64 || t == AircraftType.Mi28 || t == AircraftType.Hind ||
        t == AircraftType.UH60 || t == AircraftType.UH1;

    private void SpawnAircraft(bool randomStart = false)
    {
        // 35% chance helicopter, 65% jet
        var isHeli = _rng.NextDouble() < 0.35;
        var type = isHeli
            ? HeliTypes[_rng.Next(HeliTypes.Length)]
            : JetTypes[_rng.Next(JetTypes.Length)];

        var goingRight = _rng.NextDouble() > 0.5;
        var w = ActualWidth > 0 ? ActualWidth : 1400;
        var h = ActualHeight > 0 ? ActualHeight : 900;
        var horizonY = h * 0.55;

        double minY, maxY, speed, scale;

        if (isHeli)
        {
            // Helicopters fly lower — near and just above the horizon
            minY = horizonY * 0.30;
            maxY = horizonY * 0.52;
            var y0 = minY + _rng.NextDouble() * (maxY - minY);
            var distanceFactor = 1.0 - ((y0 - minY) / (maxY - minY));
            scale = 0.5 + distanceFactor * 0.7;
            speed = 20 + distanceFactor * 50; // slower than jets

            var startX = goingRight ? -150.0 : w + 150;
            if (randomStart) startX = _rng.NextDouble() * w;

            _aircraft.Add(new Aircraft
            {
                Type = type, X = startX, Y = y0, OriginalY = y0,
                Speed = goingRight ? speed : -speed, Scale = scale,
                GoingRight = goingRight, State = CombatState.Normal,
                ShootCooldown = 3.0 + _rng.NextDouble() * 6.0,
                MissileCooldown = 6.0 + _rng.NextDouble() * 14.0,
                Health = 1.0, HasAfterburner = false,
                IsHelicopter = true
            });
        }
        else
        {
            // Jets fly across a wider altitude range — lowered ceiling
            minY = horizonY * 0.10;
            maxY = horizonY * 0.50; // was 0.45, now lower
            var y0 = minY + _rng.NextDouble() * (maxY - minY);
            var distanceFactor = 1.0 - ((y0 - minY) / (maxY - minY));
            scale = 0.4 + distanceFactor * 0.8;
            speed = 40 + distanceFactor * 100;

            var startX = goingRight ? -150.0 : w + 150;
            if (randomStart) startX = _rng.NextDouble() * w;

            _aircraft.Add(new Aircraft
            {
                Type = type, X = startX, Y = y0, OriginalY = y0,
                Speed = goingRight ? speed : -speed, Scale = scale,
                GoingRight = goingRight, State = CombatState.Normal,
                ShootCooldown = 2.0 + _rng.NextDouble() * 5.0,
                MissileCooldown = 5.0 + _rng.NextDouble() * 12.0,
                Health = 1.0, HasAfterburner = _rng.NextDouble() < 0.25,
                IsHelicopter = false
            });
        }
    }

    private void UpdateAircraft()
    {
        var w = ActualWidth > 0 ? ActualWidth : 1400;
        var h = ActualHeight > 0 ? ActualHeight : 900;
        var dt = 0.033;

        foreach (var a in _aircraft)
        {
            switch (a.State)
            {
                case CombatState.Normal:
                    a.X += a.Speed * dt;
                    a.ShootCooldown -= dt;
                    a.MissileCooldown -= dt;

                    // Try to shoot at another aircraft
                    if (a.ShootCooldown <= 0)
                    {
                        var target = FindTarget(a);
                        if (target != null)
                        {
                            a.State = CombatState.Shooting;
                            a.ShootTimer = 0.3 + _rng.NextDouble() * 0.4; // shorter bursts
                            a.Target = target;
                            a.BurstCount = 0;
                        }
                        a.ShootCooldown = 8.0 + _rng.NextDouble() * 15.0;
                    }

                    // Try to fire a missile or drop a bomb
                    if (a.MissileCooldown <= 0)
                    {
                        if (_rng.NextDouble() < 0.6)
                        {
                            var missileTarget = FindTarget(a);
                            if (missileTarget != null)
                                FireMissile(a, missileTarget);
                        }
                        else
                        {
                            DropBomb(a);
                        }
                        a.MissileCooldown = 8.0 + _rng.NextDouble() * 15.0;
                    }

                    // 15% chance to enter dogfight loop (jets only)
                    if (!a.IsHelicopter && a.DogfightTimer <= 0 && _rng.NextDouble() < 0.005)
                    {
                        a.State = CombatState.Dogfighting;
                        a.DogfightDuration = 3.0 + _rng.NextDouble() * 4.0;
                        a.DogfightTimer = a.DogfightDuration;
                        a.LoopPhase = 0;
                        a.LoopAmplitude = 30 + _rng.NextDouble() * 50;
                    }
                    else if (a.DogfightTimer > 0)
                    {
                        a.DogfightTimer -= dt;
                    }
                    break;

                case CombatState.Shooting:
                    a.X += a.Speed * dt;
                    a.ShootTimer -= dt;
                    a.BurstTimer -= dt;

                    // Fire tracers in burst
                    if (a.BurstTimer <= 0 && a.Target != null)
                    {
                        FireTracer(a, a.Target);
                        a.BurstTimer = 0.06 + _rng.NextDouble() * 0.04;
                        a.BurstCount++;

                        // Chance to hit the target
                        if (a.BurstCount > 6 && _rng.NextDouble() < 0.12)
                        {
                            HitAircraft(a.Target);
                        }
                    }

                    if (a.ShootTimer <= 0)
                    {
                        a.State = CombatState.Normal;
                        a.Target = null;
                    }
                    break;

                case CombatState.Hit:
                    a.X += a.Speed * dt * 0.7;
                    a.HitTimer -= dt;

                    // Emit smoke
                    if (_rng.NextDouble() < 0.6)
                        EmitSmoke(a);

                    if (a.HitTimer <= 0)
                    {
                        // Either recover or go down
                        if (_rng.NextDouble() < 0.5)
                        {
                            a.State = CombatState.GoingDown;
                            a.FallSpeed = 15 + _rng.NextDouble() * 25;
                            a.SpinRate = (_rng.NextDouble() - 0.5) * 3.0;
                        }
                        else
                        {
                            a.State = CombatState.Smoking;
                            a.SmokeTimer = 4.0 + _rng.NextDouble() * 5.0;
                        }
                    }
                    break;

                case CombatState.Smoking:
                    a.X += a.Speed * dt * 0.85;
                    a.SmokeTimer -= dt;

                    if (_rng.NextDouble() < 0.4)
                        EmitSmoke(a);

                    if (a.SmokeTimer <= 0)
                        a.State = CombatState.Normal; // recovered
                    break;

                case CombatState.Dogfighting:
                    a.X += a.Speed * dt;
                    a.DogfightTimer -= dt;
                    a.LoopPhase += dt * 2.5; // loop speed

                    // Sinusoidal up/down loop motion
                    a.Y = a.OriginalY + Math.Sin(a.LoopPhase) * a.LoopAmplitude * a.Scale;
                    // Tilt the aircraft during the loop
                    a.Rotation = Math.Cos(a.LoopPhase) * 25; // degrees of pitch

                    if (a.DogfightTimer <= 0)
                    {
                        a.State = CombatState.Normal;
                        a.Y = a.OriginalY;
                        a.Rotation = 0;
                        a.DogfightTimer = 10.0 + _rng.NextDouble() * 15.0; // cooldown
                    }
                    break;

                case CombatState.GoingDown:
                    a.X += a.Speed * dt * 0.5;
                    a.FallSpeed += 30 * dt; // accelerate downward
                    a.Y += a.FallSpeed * dt;
                    a.Rotation += a.SpinRate * dt;

                    // Heavy smoke
                    if (_rng.NextDouble() < 0.8)
                        EmitSmoke(a, heavy: true);

                    // Explode when hitting the ground
                    if (a.Y > h * 0.55)
                    {
                        SpawnExplosion(a.X, h * 0.55, a.Scale);
                        a.State = CombatState.Dead;
                    }
                    break;
            }
        }

        // Remove dead and off-screen aircraft
        _aircraft.RemoveAll(a =>
            a.State == CombatState.Dead ||
            (a.GoingRight && a.X > w + 300) ||
            (!a.GoingRight && a.X < -300) ||
            a.Y > h + 100);

        // Keep 3-5 aircraft in the scene
        if (_aircraft.Count < 3 && _rng.NextDouble() < 0.025)
            SpawnAircraft();
        else if (_aircraft.Count < 4 && _rng.NextDouble() < 0.01)
            SpawnAircraft();
        else if (_aircraft.Count < 5 && _rng.NextDouble() < 0.005)
            SpawnAircraft();
    }

    private Aircraft? FindTarget(Aircraft shooter)
    {
        // Find an aircraft flying in the opposite direction or ahead of this one
        foreach (var other in _aircraft)
        {
            if (other == shooter) continue;
            if (other.State == CombatState.Dead || other.State == CombatState.GoingDown) continue;

            // Target should be somewhat in front of the shooter
            var dx = other.X - shooter.X;
            if (shooter.GoingRight && dx > 50 && dx < 500)
                return other;
            if (!shooter.GoingRight && dx < -50 && dx > -500)
                return other;
        }
        return null;
    }

    private void HitAircraft(Aircraft target)
    {
        if (target.State == CombatState.GoingDown || target.State == CombatState.Dead) return;

        target.Health -= 0.3 + _rng.NextDouble() * 0.4;

        if (target.Health <= 0)
        {
            target.State = CombatState.GoingDown;
            target.FallSpeed = 10 + _rng.NextDouble() * 20;
            target.SpinRate = (_rng.NextDouble() - 0.5) * 4.0;
            SpawnExplosion(target.X, target.Y, target.Scale * 0.6);
        }
        else
        {
            target.State = CombatState.Hit;
            target.HitTimer = 0.8 + _rng.NextDouble() * 1.2;
            SpawnExplosion(target.X, target.Y, target.Scale * 0.3);
        }
    }

    // ───────────────────────────────────────────────────────────────
    //  TRACERS
    // ───────────────────────────────────────────────────────────────

    private void FireTracer(Aircraft shooter, Aircraft target)
    {
        var flip = shooter.GoingRight ? 1.0 : -1.0;
        var s = shooter.Scale;

        // Tracer starts from nose of aircraft
        var startX = shooter.X + 40 * s * flip;
        var startY = shooter.Y;

        // Aim toward target with some spread
        var aimX = target.X + (_rng.NextDouble() - 0.5) * 40;
        var aimY = target.Y + (_rng.NextDouble() - 0.5) * 30;

        var dx = aimX - startX;
        var dy = aimY - startY;
        var dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist < 1) dist = 1;

        var tracerSpeed = 800 + _rng.NextDouble() * 400;

        _tracers.Add(new Tracer
        {
            X = startX,
            Y = startY,
            VX = (dx / dist) * tracerSpeed,
            VY = (dy / dist) * tracerSpeed,
            Life = 0.4 + _rng.NextDouble() * 0.3,
            Scale = s
        });
    }

    private void UpdateTracers()
    {
        var dt = 0.033;
        foreach (var t in _tracers)
        {
            t.X += t.VX * dt;
            t.Y += t.VY * dt;
            t.Life -= dt;
        }
        _tracers.RemoveAll(t => t.Life <= 0);
    }

    // ───────────────────────────────────────────────────────────────
    //  MISSILES & BOMBS
    // ───────────────────────────────────────────────────────────────

    private void FireMissile(Aircraft shooter, Aircraft target)
    {
        var flip = shooter.GoingRight ? 1.0 : -1.0;
        var s = shooter.Scale;
        var startX = shooter.X + 30 * s * flip;
        var startY = shooter.Y + 8 * s;

        _missiles.Add(new Missile
        {
            X = startX,
            Y = startY,
            TargetAircraft = target,
            Speed = 250 + shooter.Scale * 200,
            Scale = s,
            Life = 3.0,
            GoingRight = shooter.GoingRight
        });
    }

    private void DropBomb(Aircraft bomber)
    {
        var s = bomber.Scale;
        var h = ActualHeight > 0 ? ActualHeight : 900;
        var horizonY = h * 0.55;

        _missiles.Add(new Missile
        {
            X = bomber.X,
            Y = bomber.Y + 5 * s,
            TargetAircraft = null, // bomb — no air target
            TargetGroundX = bomber.X + (bomber.GoingRight ? 80 : -80) * s,
            TargetGroundY = horizonY,
            Speed = 120,
            Scale = s,
            Life = 3.0,
            IsBomb = true,
            VY = 0,
            GoingRight = bomber.GoingRight
        });
    }

    private void UpdateMissiles()
    {
        var dt = 0.033;
        var w = ActualWidth > 0 ? ActualWidth : 1400;
        var h = ActualHeight > 0 ? ActualHeight : 900;
        var horizonY = h * 0.55;
        var centerX = w * 0.5;
        var centerY = h * 0.5;

        foreach (var m in _missiles)
        {
            m.Life -= dt;

            if (m.IsBomb)
            {
                // Bomb falls with gravity
                m.VY += 180 * dt;
                m.X += (m.GoingRight ? 40 : -40) * dt;
                m.Y += m.VY * dt;

                // Emit small smoke trail
                if (_rng.NextDouble() < 0.3)
                {
                    _smoke.Add(new SmokeParticle
                    {
                        X = m.X, Y = m.Y,
                        VX = (_rng.NextDouble() - 0.5) * 5,
                        VY = -8,
                        Life = 0.5, MaxLife = 0.5,
                        Size = 3 * m.Scale, IsFire = false
                    });
                }

                // Hit the ground
                if (m.Y >= horizonY)
                {
                    SpawnExplosion(m.X, horizonY, m.Scale * 1.5);
                    m.Life = 0;
                }
            }
            else if (m.TargetAircraft != null)
            {
                // Guided missile — track target
                var target = m.TargetAircraft;
                var dx = target.X - m.X;
                var dy = target.Y - m.Y;
                var dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist < 15)
                {
                    // 5% chance the missile misses and flies toward the viewer
                    if (_rng.NextDouble() < 0.05)
                    {
                        // Spawn incoming missile toward center screen
                        _incomingMissiles.Add(new IncomingMissile
                        {
                            X = m.X,
                            Y = m.Y,
                            StartX = m.X,
                            StartY = m.Y,
                            TargetX = centerX + (_rng.NextDouble() - 0.5) * 100,
                            TargetY = centerY + (_rng.NextDouble() - 0.5) * 60,
                            Progress = 0,
                            Duration = 1.8 + _rng.NextDouble() * 1.0,
                            Scale = m.Scale
                        });
                    }
                    else
                    {
                        // Hit!
                        HitAircraft(target);
                        SpawnExplosion(target.X, target.Y, m.Scale * 0.8);
                    }
                    m.Life = 0;
                }
                else if (dist > 0)
                {
                    var spd = m.Speed;
                    m.X += (dx / dist) * spd * dt;
                    m.Y += (dy / dist) * spd * dt;
                }

                // Missile smoke trail
                if (_rng.NextDouble() < 0.5)
                {
                    _smoke.Add(new SmokeParticle
                    {
                        X = m.X, Y = m.Y,
                        VX = (_rng.NextDouble() - 0.5) * 8,
                        VY = (_rng.NextDouble() - 0.5) * 8,
                        Life = 0.6, MaxLife = 0.6,
                        Size = 2.5 * m.Scale, IsFire = _rng.NextDouble() < 0.2
                    });
                }
            }
            else
            {
                // SAM missile — fly toward TargetGroundX/Y coordinates
                var dx = m.TargetGroundX - m.X;
                var dy = m.TargetGroundY - m.Y;
                var dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist < 20)
                {
                    // Reached target area — check if any aircraft is nearby
                    bool hitSomething = false;
                    foreach (var a in _aircraft)
                    {
                        if (a.State == CombatState.Dead || a.State == CombatState.GoingDown) continue;
                        var adx = a.X - m.X;
                        var ady = a.Y - m.Y;
                        if (Math.Sqrt(adx * adx + ady * ady) < 30)
                        {
                            HitAircraft(a);
                            SpawnExplosion(a.X, a.Y, m.Scale * 0.8);
                            hitSomething = true;
                            break;
                        }
                    }
                    if (!hitSomething)
                    {
                        // Miss — small puff
                        SpawnExplosion(m.X, m.Y, m.Scale * 0.3);
                    }
                    m.Life = 0;
                }
                else if (dist > 0)
                {
                    var spd = m.Speed;
                    m.X += (dx / dist) * spd * dt;
                    m.Y += (dy / dist) * spd * dt;
                }

                // SAM missile smoke trail
                if (_rng.NextDouble() < 0.5)
                {
                    _smoke.Add(new SmokeParticle
                    {
                        X = m.X, Y = m.Y,
                        VX = (_rng.NextDouble() - 0.5) * 8,
                        VY = (_rng.NextDouble() - 0.5) * 8,
                        Life = 0.6, MaxLife = 0.6,
                        Size = 2.5 * m.Scale, IsFire = _rng.NextDouble() < 0.15
                    });
                }
            }
        }

        _missiles.RemoveAll(m => m.Life <= 0);
    }

    private void UpdateIncomingMissiles()
    {
        var dt = 0.033;
        var w = ActualWidth > 0 ? ActualWidth : 1400;
        var h = ActualHeight > 0 ? ActualHeight : 900;

        foreach (var im in _incomingMissiles)
        {
            im.Progress += dt / im.Duration;

            // Ease-in: accelerates as it gets closer
            var t = im.Progress * im.Progress;
            im.X = im.StartX + (im.TargetX - im.StartX) * t;
            im.Y = im.StartY + (im.TargetY - im.StartY) * t;
            im.CurrentScale = im.Scale + t * 8.0; // grows from tiny to huge

            // Smoke trail
            if (_rng.NextDouble() < 0.7)
            {
                _smoke.Add(new SmokeParticle
                {
                    X = im.X + (_rng.NextDouble() - 0.5) * im.CurrentScale * 3,
                    Y = im.Y + (_rng.NextDouble() - 0.5) * im.CurrentScale * 2,
                    VX = (_rng.NextDouble() - 0.5) * 20,
                    VY = (_rng.NextDouble() - 0.5) * 20,
                    Life = 0.4 + _rng.NextDouble() * 0.4,
                    MaxLife = 0.8,
                    Size = im.CurrentScale * 1.5,
                    IsFire = _rng.NextDouble() < 0.4
                });
            }

            // When it "reaches" the viewer, big flash then gone
            if (im.Progress >= 1.0)
            {
                SpawnExplosion(im.X, im.Y, im.CurrentScale * 0.5);
            }
        }

        _incomingMissiles.RemoveAll(im => im.Progress >= 1.0);
    }

    // ───────────────────────────────────────────────────────────────
    //  EXPLOSIONS
    // ───────────────────────────────────────────────────────────────

    private void SpawnExplosion(double x, double y, double scale)
    {
        _explosions.Add(new Explosion
        {
            X = x,
            Y = y,
            Scale = scale,
            Life = 1.0,
            MaxLife = 1.0
        });
    }

    private void UpdateExplosions()
    {
        var dt = 0.033;
        foreach (var e in _explosions)
            e.Life -= dt;
        _explosions.RemoveAll(e => e.Life <= 0);
    }

    // ───────────────────────────────────────────────────────────────
    //  SMOKE PARTICLES
    // ───────────────────────────────────────────────────────────────

    private void EmitSmoke(Aircraft a, bool heavy = false)
    {
        var flip = a.GoingRight ? 1.0 : -1.0;
        _smoke.Add(new SmokeParticle
        {
            X = a.X - 20 * a.Scale * flip + (_rng.NextDouble() - 0.5) * 10,
            Y = a.Y + (_rng.NextDouble() - 0.5) * 6,
            VX = -a.Speed * 0.15 + (_rng.NextDouble() - 0.5) * 15,
            VY = -5 - _rng.NextDouble() * 10,
            Life = heavy ? 1.5 + _rng.NextDouble() * 1.5 : 0.8 + _rng.NextDouble() * 1.0,
            MaxLife = heavy ? 2.5 : 1.5,
            Size = (heavy ? 8 : 4) + _rng.NextDouble() * (heavy ? 12 : 6),
            IsFire = heavy && _rng.NextDouble() < 0.35
        });
    }

    private void UpdateSmoke()
    {
        var dt = 0.033;
        foreach (var s in _smoke)
        {
            s.X += s.VX * dt;
            s.Y += s.VY * dt;
            s.VY -= 3 * dt; // drift upward
            s.Size += 8 * dt; // expand
            s.Life -= dt;
        }
        _smoke.RemoveAll(s => s.Life <= 0);

        // Cap smoke particles
        while (_smoke.Count > 200)
            _smoke.RemoveAt(0);
    }

    // ───────────────────────────────────────────────────────────────
    //  MAIN RENDER
    // ───────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var horizonY = h * 0.55;

        DrawSky(dc, w, horizonY);
        DrawSun(dc, w, horizonY);
        DrawMountains(dc, w, horizonY);
        DrawGround(dc, w, h, horizonY);
        DrawGrid(dc, w, h, horizonY);
        DrawPalmTrees(dc, w, horizonY);
        DrawSamSites(dc, w, h, horizonY);
        DrawAllSmoke(dc);
        DrawAllAircraft(dc);
        DrawAllTracers(dc);
        DrawAllMissiles(dc);
        DrawAllExplosions(dc);
        DrawAllIncomingMissiles(dc);
        DrawFade(dc, w, h);
    }

    // ───────────────────────────────────────────────────────────────
    //  SKY
    // ───────────────────────────────────────────────────────────────

    private static void DrawSky(DrawingContext dc, double w, double horizonY)
    {
        var skyBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1),
            GradientStops = new GradientStopCollection
            {
                new(Color.FromRgb(8, 6, 20), 0.0),
                new(Color.FromRgb(20, 10, 50), 0.3),
                new(Color.FromRgb(60, 15, 80), 0.55),
                new(Color.FromRgb(140, 30, 70), 0.75),
                new(Color.FromRgb(220, 80, 40), 0.9),
                new(Color.FromRgb(255, 160, 50), 1.0),
            }
        };
        skyBrush.Freeze();
        dc.DrawRectangle(skyBrush, null, new Rect(0, 0, w, horizonY));
    }

    // ───────────────────────────────────────────────────────────────
    //  SUN (with stripe gaps)
    // ───────────────────────────────────────────────────────────────

    private void DrawSun(DrawingContext dc, double w, double horizonY)
    {
        var sunRadius = Math.Min(w, horizonY) * 0.15;
        var pulse = sunRadius + Math.Sin(_time * 0.5) * 2;
        var center = new Point(w * 0.5, horizonY - sunRadius * 0.25);

        var glow = new RadialGradientBrush
        {
            GradientStops = new GradientStopCollection
            {
                new(Color.FromArgb(70, 255, 160, 50), 0.0),
                new(Color.FromArgb(35, 255, 80, 60), 0.5),
                new(Color.FromArgb(0, 255, 40, 80), 1.0),
            }
        };
        glow.Freeze();
        dc.DrawEllipse(glow, null, center, pulse * 2.8, pulse * 2.8);

        var sunGrad = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1),
            GradientStops = new GradientStopCollection
            {
                new(Color.FromRgb(255, 220, 80), 0.0),
                new(Color.FromRgb(255, 140, 50), 0.4),
                new(Color.FromRgb(255, 60, 80), 0.8),
                new(Color.FromRgb(200, 30, 80), 1.0),
            }
        };
        sunGrad.Freeze();

        dc.PushClip(new EllipseGeometry(center, pulse, pulse));
        dc.DrawEllipse(sunGrad, null, center, pulse, pulse);

        var stripeBrush = new SolidColorBrush(Color.FromArgb(200, 8, 6, 20));
        stripeBrush.Freeze();
        for (int i = 1; i <= 7; i++)
        {
            var stripeY = center.Y - pulse + (pulse * 2.0 * i / 8.0);
            var thickness = 1.5 + i * 1.2;
            var pen = new Pen(stripeBrush, thickness);
            pen.Freeze();
            dc.DrawLine(pen, new Point(center.X - pulse - 5, stripeY),
                             new Point(center.X + pulse + 5, stripeY));
        }
        dc.Pop();
    }

    // ───────────────────────────────────────────────────────────────
    //  MOUNTAINS
    // ───────────────────────────────────────────────────────────────

    private static void DrawMountains(DrawingContext dc, double w, double horizonY)
    {
        var brush1 = new SolidColorBrush(Color.FromRgb(30, 10, 50));
        brush1.Freeze();
        DrawRange(dc, brush1, w, horizonY, new[]
        {
            (0.0, 0.7), (0.06, 0.35), (0.12, 0.55), (0.20, 0.25), (0.28, 0.5),
            (0.36, 0.15), (0.44, 0.38), (0.52, 0.20), (0.60, 0.45), (0.68, 0.30),
            (0.76, 0.50), (0.84, 0.22), (0.92, 0.48), (1.0, 0.65)
        });

        var brush2 = new SolidColorBrush(Color.FromRgb(20, 6, 35));
        brush2.Freeze();
        DrawRange(dc, brush2, w, horizonY, new[]
        {
            (0.0, 0.8), (0.08, 0.55), (0.16, 0.65), (0.24, 0.42), (0.32, 0.58),
            (0.40, 0.48), (0.50, 0.38), (0.58, 0.52), (0.66, 0.42), (0.74, 0.56),
            (0.82, 0.46), (0.90, 0.58), (1.0, 0.72)
        });
    }

    private static void DrawRange(DrawingContext dc, Brush brush, double w, double horizonY,
        (double x, double hf)[] peaks)
    {
        var maxH = horizonY * 0.4;
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(0, horizonY), true, true);
            foreach (var (x, hf) in peaks)
                ctx.LineTo(new Point(x * w, horizonY - hf * maxH), true, false);
            ctx.LineTo(new Point(w, horizonY), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(brush, null, geo);
    }

    // ───────────────────────────────────────────────────────────────
    //  GROUND
    // ───────────────────────────────────────────────────────────────

    private static void DrawGround(DrawingContext dc, double w, double h, double horizonY)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1),
            GradientStops = new GradientStopCollection
            {
                new(Color.FromRgb(20, 5, 40), 0.0),
                new(Color.FromRgb(10, 3, 25), 0.5),
                new(Color.FromRgb(5, 2, 15), 1.0),
            }
        };
        brush.Freeze();
        dc.DrawRectangle(brush, null, new Rect(0, horizonY, w, h - horizonY));
    }

    // ───────────────────────────────────────────────────────────────
    //  PERSPECTIVE GRID — smooth seamless scrolling
    // ───────────────────────────────────────────────────────────────

    private void DrawGrid(DrawingContext dc, double w, double h, double horizonY)
    {
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(90, 180, 50, 255)), 1);
        gridPen.Freeze();
        var gridPenFaint = new Pen(new SolidColorBrush(Color.FromArgb(40, 140, 30, 200)), 0.5);
        gridPenFaint.Freeze();

        var vanishX = w * 0.5;
        var groundH = h - horizonY;

        var scrollSpeed = 0.15;
        var totalLines = 30;
        var spacing = 1.0 / totalLines;
        var offset = (_time * scrollSpeed) % spacing;

        for (int i = 0; i < totalLines; i++)
        {
            var t = offset + i * spacing;
            if (t > 1.0) t -= 1.0;
            if (t < 0.0 || t > 1.0) continue;

            var perspY = t * t;
            var y = horizonY + perspY * groundH;

            if (y <= horizonY || y >= h) continue;

            var alpha = (byte)(40 + t * 80);
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, 180, 50, 255)), 0.5 + t * 1.0);
            pen.Freeze();
            dc.DrawLine(pen, new Point(0, y), new Point(w, y));
        }

        var vLines = 20;
        for (int i = -vLines; i <= vLines; i++)
        {
            var bottomX = vanishX + i * (w / vLines) * 1.3;
            var isBright = (i % 4 == 0);
            var pen = isBright ? gridPen : gridPenFaint;
            dc.DrawLine(pen, new Point(vanishX, horizonY), new Point(bottomX, h));
        }
    }

    // ───────────────────────────────────────────────────────────────
    //  SAM SITES
    // ───────────────────────────────────────────────────────────────

    private void UpdateSamSites()
    {
        var w = ActualWidth > 0 ? ActualWidth : 1400;
        var h = ActualHeight > 0 ? ActualHeight : 900;
        var horizonY = h * 0.55;
        var dt = 0.033;

        if (!_samsInitialized)
        {
            _samLeft.X = w * 0.12;
            _samLeft.Y = horizonY;
            _samLeft.RadarAngle = 0;
            _samLeft.RadarSpeed = 35 + _rng.NextDouble() * 20;
            _samLeft.FireCooldown = 8 + _rng.NextDouble() * 12;
            _samLeft.MuzzleFlash = 0;

            _samRight.X = w * 0.88;
            _samRight.Y = horizonY;
            _samRight.RadarAngle = 180;
            _samRight.RadarSpeed = 30 + _rng.NextDouble() * 25;
            _samRight.FireCooldown = 12 + _rng.NextDouble() * 10;
            _samRight.MuzzleFlash = 0;

            _samsInitialized = true;
        }

        // Keep positions relative to window width
        _samLeft.X = w * 0.12;
        _samRight.X = w * 0.88;
        _samLeft.Y = horizonY;
        _samRight.Y = horizonY;

        UpdateSingleSam(_samLeft, dt, w, horizonY);
        UpdateSingleSam(_samRight, dt, w, horizonY);
    }

    private void UpdateSingleSam(SamSite sam, double dt, double w, double horizonY)
    {
        // Rotate radar continuously
        sam.RadarAngle += sam.RadarSpeed * dt;
        if (sam.RadarAngle > 360) sam.RadarAngle -= 360;

        // Fire cooldown
        sam.FireCooldown -= dt;
        if (sam.FireCooldown <= 0)
        {
            // Launch a missile upward toward a random sky position
            var targetX = w * (0.2 + _rng.NextDouble() * 0.6);
            var targetY = horizonY * (0.05 + _rng.NextDouble() * 0.3);

            // Find a real aircraft to aim at if possible
            foreach (var a in _aircraft)
            {
                if (a.State == CombatState.Dead || a.State == CombatState.GoingDown) continue;
                if (Math.Abs(a.X - sam.X) < w * 0.5)
                {
                    targetX = a.X;
                    targetY = a.Y;
                    break;
                }
            }

            var launchX = sam.X;
            var launchY = sam.Y - 18; // top of launcher

            var dx = targetX - launchX;
            var dy = targetY - launchY;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < 1) dist = 1;

            var missileSpeed = 300 + _rng.NextDouble() * 200;

            _missiles.Add(new Missile
            {
                X = launchX,
                Y = launchY,
                TargetAircraft = null,
                TargetGroundX = targetX,
                TargetGroundY = targetY,
                Speed = missileSpeed,
                Scale = 0.8,
                Life = 4.0,
                IsBomb = false,
                VY = (dy / dist) * missileSpeed,
                GoingRight = dx > 0
            });

            // Override — make it a tracking missile by giving it velocity
            // We'll handle it via the general missile system
            // Actually, let's add it as a proper SAM missile
            _tracers.Add(new Tracer
            {
                X = launchX,
                Y = launchY,
                VX = (dx / dist) * missileSpeed,
                VY = (dy / dist) * missileSpeed,
                Life = 3.0,
                Scale = 1.0
            });

            sam.MuzzleFlash = 0.3;
            sam.FireCooldown = 10 + _rng.NextDouble() * 18;

            // Launch smoke
            for (int i = 0; i < 10; i++)
            {
                _smoke.Add(new SmokeParticle
                {
                    X = launchX + (_rng.NextDouble() - 0.5) * 12,
                    Y = launchY + _rng.NextDouble() * 8,
                    VX = (_rng.NextDouble() - 0.5) * 30,
                    VY = 10 + _rng.NextDouble() * 20,
                    Life = 1.0 + _rng.NextDouble() * 1.0,
                    MaxLife = 2.0,
                    Size = 5 + _rng.NextDouble() * 8,
                    IsFire = i < 3
                });
            }

            // Spawn ground-level explosion flash at launch
            SpawnExplosion(launchX, launchY, 0.5);
        }

        if (sam.MuzzleFlash > 0)
            sam.MuzzleFlash -= 0.033;
    }

    private void DrawSamSites(DrawingContext dc, double w, double h, double horizonY)
    {
        if (!_samsInitialized) return;
        DrawSingleSam(dc, _samLeft, false);
        DrawSingleSam(dc, _samRight, true);
    }

    private void DrawSingleSam(DrawingContext dc, SamSite sam, bool mirrorSide)
    {
        var cx = sam.X;
        var groundY = sam.Y;
        var s = 0.9;

        var darkBrush = new SolidColorBrush(Color.FromRgb(12, 7, 24));
        darkBrush.Freeze();
        var medBrush = new SolidColorBrush(Color.FromRgb(18, 12, 35));
        medBrush.Freeze();
        var detailPen = new Pen(new SolidColorBrush(Color.FromArgb(50, 50, 35, 80)), 0.8);
        detailPen.Freeze();

        // ── LAUNCHER VEHICLE BASE ──
        var baseGeo = new StreamGeometry();
        using (var ctx = baseGeo.Open())
        {
            ctx.BeginFigure(new Point(cx - 22 * s, groundY + 2), true, true);
            ctx.LineTo(new Point(cx - 20 * s, groundY - 6 * s), true, false);
            ctx.LineTo(new Point(cx - 16 * s, groundY - 10 * s), true, false);
            ctx.LineTo(new Point(cx + 16 * s, groundY - 10 * s), true, false);
            ctx.LineTo(new Point(cx + 20 * s, groundY - 6 * s), true, false);
            ctx.LineTo(new Point(cx + 22 * s, groundY + 2), true, false);
        }
        baseGeo.Freeze();
        dc.DrawGeometry(darkBrush, null, baseGeo);

        // Wheels (3 per side)
        var wheelBrush = new SolidColorBrush(Color.FromRgb(8, 5, 16));
        wheelBrush.Freeze();
        for (int i = 0; i < 3; i++)
        {
            var wheelX = cx - 14 * s + i * 14 * s;
            dc.DrawEllipse(wheelBrush, null, new Point(wheelX, groundY + 1), 5 * s, 4.5 * s);
        }

        // ── MISSILE RAIL (angled upward) ──
        var railAngle = -65 * Math.PI / 180.0; // angled up
        var railLen = 20 * s;
        var railBaseX = cx;
        var railBaseY = groundY - 10 * s;
        var railTopX = railBaseX + Math.Cos(railAngle) * railLen * (mirrorSide ? -0.3 : 0.3);
        var railTopY = railBaseY + Math.Sin(railAngle) * railLen;

        var railPen = new Pen(medBrush, 4 * s);
        railPen.Freeze();
        dc.DrawLine(railPen, new Point(railBaseX, railBaseY), new Point(railTopX, railTopY));

        // Missile tubes on rail (4 tubes)
        var tubePen = new Pen(darkBrush, 2.5 * s);
        tubePen.Freeze();
        for (int i = 0; i < 4; i++)
        {
            var frac = 0.3 + i * 0.18;
            var tx = railBaseX + (railTopX - railBaseX) * frac + (i % 2 == 0 ? -2 : 2) * s;
            var ty = railBaseY + (railTopY - railBaseY) * frac;
            var tipX = tx + Math.Cos(railAngle) * 8 * s * (mirrorSide ? -0.3 : 0.3);
            var tipY = ty + Math.Sin(railAngle) * 8 * s;
            dc.DrawLine(tubePen, new Point(tx, ty), new Point(tipX, tipY));
        }

        // ── RADAR DISH (spinning) ──
        var radarMastX = cx + (mirrorSide ? -8 : 8) * s;
        var radarMastY = groundY - 10 * s;
        var radarTopY = radarMastY - 16 * s;

        // Mast
        var mastPen = new Pen(medBrush, 2 * s);
        mastPen.Freeze();
        dc.DrawLine(mastPen, new Point(radarMastX, radarMastY), new Point(radarMastX, radarTopY));

        // Spinning radar — flat panel that rotates (appears as line from side)
        var radarRad = sam.RadarAngle * Math.PI / 180.0;
        var radarWidth = 14 * s;
        var radarProjX = Math.Cos(radarRad) * radarWidth; // horizontal projection
        var radarThickness = Math.Abs(Math.Sin(radarRad)) * 3 * s + 1; // depth illusion

        var radarPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 20, 14, 40)), radarThickness);
        radarPen.Freeze();
        dc.DrawLine(radarPen,
            new Point(radarMastX - radarProjX, radarTopY),
            new Point(radarMastX + radarProjX, radarTopY));

        // Radar sweep glow
        var sweepAlpha = (byte)(60 + Math.Abs(Math.Sin(radarRad)) * 40);
        var sweepBrush = new SolidColorBrush(Color.FromArgb(sweepAlpha, 0, 200, 100));
        sweepBrush.Freeze();
        dc.DrawEllipse(sweepBrush, null, new Point(radarMastX, radarTopY), 3 * s, 3 * s);

        // ── Launch flash ──
        if (sam.MuzzleFlash > 0)
        {
            var intensity = sam.MuzzleFlash / 0.3;
            var flashAlpha = (byte)(200 * intensity);
            var flashSize = 15 + (1 - intensity) * 20;

            var flashBrush = new RadialGradientBrush
            {
                GradientStops = new GradientStopCollection
                {
                    new(Color.FromArgb(flashAlpha, 255, 255, 200), 0.0),
                    new(Color.FromArgb((byte)(flashAlpha * 0.5), 255, 160, 40), 0.4),
                    new(Color.FromArgb(0, 255, 80, 10), 1.0),
                }
            };
            flashBrush.Freeze();
            dc.DrawEllipse(flashBrush, null, new Point(railTopX, railTopY), flashSize, flashSize * 0.8);
        }
    }

    // ───────────────────────────────────────────────────────────────
    //  PALM TREES
    // ───────────────────────────────────────────────────────────────

    private void DrawPalmTrees(DrawingContext dc, double w, double horizonY)
    {
        var palms = new (double xFrac, double scale, bool leanRight)[]
        {
            // Dense left cluster
            (0.01, 0.50, false), (0.03, 0.90, true), (0.05, 0.35, false),
            (0.07, 0.75, true), (0.09, 0.60, false), (0.11, 0.40, true),
            (0.13, 0.85, false), (0.15, 0.55, true), (0.17, 0.70, false),
            (0.19, 0.45, true), (0.21, 0.65, false), (0.23, 0.80, true),
            (0.25, 0.35, false), (0.27, 0.55, true), (0.29, 0.42, false),
            (0.31, 0.68, true), (0.33, 0.30, false), (0.35, 0.50, true),
            (0.37, 0.75, false), (0.39, 0.40, true),
            // Scattered middle
            (0.42, 0.30, false), (0.45, 0.25, true), (0.48, 0.28, false),
            (0.52, 0.28, true), (0.55, 0.25, false), (0.58, 0.30, true),
            // Dense right cluster
            (0.61, 0.40, false), (0.63, 0.75, true), (0.65, 0.50, false),
            (0.67, 0.30, true), (0.69, 0.68, false), (0.71, 0.42, true),
            (0.73, 0.55, false), (0.75, 0.35, true), (0.77, 0.80, false),
            (0.79, 0.65, true), (0.81, 0.45, false), (0.83, 0.70, true),
            (0.85, 0.55, false), (0.87, 0.40, true), (0.89, 0.85, false),
            (0.91, 0.60, true), (0.93, 0.75, false), (0.95, 0.35, true),
            (0.97, 0.90, false), (0.99, 0.50, true),
        };

        foreach (var (xFrac, scale, lean) in palms)
            DrawPalmTree(dc, w * xFrac, horizonY, scale, lean);
    }

    private void DrawPalmTree(DrawingContext dc, double baseX, double horizonY, double scale, bool leanRight)
    {
        var treeHeight = 170 * scale;
        var trunkBottom = new Point(baseX, horizonY + 8);
        var lean = (leanRight ? 25 : -25) * scale;
        var sway = Math.Sin(_time * 0.8 + baseX * 0.01) * 4 * scale;
        var trunkTop = new Point(baseX + lean + sway, horizonY - treeHeight);

        var trunkBrush = new SolidColorBrush(Color.FromRgb(12, 5, 22));
        trunkBrush.Freeze();
        var trunkPen = new Pen(trunkBrush, 5 * scale);
        trunkPen.Freeze();

        var trunkGeo = new StreamGeometry();
        using (var ctx = trunkGeo.Open())
        {
            ctx.BeginFigure(trunkBottom, false, false);
            var midPoint = new Point(baseX + lean * 0.4 + sway * 0.3, horizonY - treeHeight * 0.5);
            ctx.QuadraticBezierTo(midPoint, trunkTop, true, false);
        }
        trunkGeo.Freeze();
        dc.DrawGeometry(null, trunkPen, trunkGeo);

        var frondBrush = new SolidColorBrush(Color.FromRgb(10, 4, 20));
        frondBrush.Freeze();

        double[] angles = { -80, -55, -35, -15, 10, 30, 50, 70 };
        foreach (var angleDeg in angles)
        {
            var rad = angleDeg * Math.PI / 180.0;
            var frondLen = 55 * scale;
            var endX = trunkTop.X + Math.Cos(rad) * frondLen;
            var endY = trunkTop.Y + Math.Sin(rad) * frondLen * 0.4 - 8 * scale;
            var droopY = endY + 18 * scale;
            var frondSway = Math.Sin(_time * 1.2 + angleDeg * 0.1) * 3 * scale;

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(trunkTop, true, true);
                ctx.BezierTo(
                    new Point(trunkTop.X + (endX - trunkTop.X) * 0.3 + frondSway, trunkTop.Y - 12 * scale),
                    new Point(endX + frondSway, endY),
                    new Point(endX + frondSway, droopY),
                    true, false);
                ctx.LineTo(new Point(endX - 4 * scale * Math.Cos(rad + 0.3) + frondSway, droopY + 4 * scale), true, false);
                ctx.BezierTo(
                    new Point(endX - 6 * scale + frondSway, endY + 6 * scale),
                    new Point(trunkTop.X + (endX - trunkTop.X) * 0.15, trunkTop.Y),
                    trunkTop,
                    true, false);
            }
            geo.Freeze();
            dc.DrawGeometry(frondBrush, null, geo);
        }
    }

    // ───────────────────────────────────────────────────────────────
    //  DRAW SMOKE
    // ───────────────────────────────────────────────────────────────

    private void DrawAllSmoke(DrawingContext dc)
    {
        foreach (var s in _smoke)
        {
            var fade = Math.Max(0, s.Life / s.MaxLife);
            var alpha = (byte)(fade * (s.IsFire ? 160 : 80));
            var size = s.Size;

            Brush brush;
            if (s.IsFire)
            {
                // Orange/yellow fire
                var r = (byte)(255 * fade);
                var g = (byte)(120 * fade + 40);
                brush = new SolidColorBrush(Color.FromArgb(alpha, r, g, 20));
            }
            else
            {
                // Dark smoke
                var gray = (byte)(30 + 20 * (1 - fade));
                brush = new SolidColorBrush(Color.FromArgb(alpha, gray, gray, (byte)(gray + 10)));
            }
            brush.Freeze();

            dc.DrawEllipse(brush, null, new Point(s.X, s.Y), size, size * 0.7);
        }
    }

    // ───────────────────────────────────────────────────────────────
    //  DRAW TRACERS
    // ───────────────────────────────────────────────────────────────

    private void DrawAllTracers(DrawingContext dc)
    {
        foreach (var t in _tracers)
        {
            var fade = Math.Max(0, Math.Min(1, t.Life / 0.3));
            var alpha = (byte)(255 * fade);

            // Bright yellow-orange tracer
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, 255, 200, 50)), 1.5 * t.Scale);
            pen.Freeze();

            // Draw a short line in direction of travel
            var len = 12 * t.Scale;
            var speed = Math.Sqrt(t.VX * t.VX + t.VY * t.VY);
            if (speed < 1) speed = 1;
            var tailX = t.X - (t.VX / speed) * len;
            var tailY = t.Y - (t.VY / speed) * len;

            dc.DrawLine(pen, new Point(tailX, tailY), new Point(t.X, t.Y));

            // Glow dot at tip
            var glowBrush = new SolidColorBrush(Color.FromArgb((byte)(alpha * 0.5), 255, 255, 180));
            glowBrush.Freeze();
            dc.DrawEllipse(glowBrush, null, new Point(t.X, t.Y), 2.5 * t.Scale, 2.5 * t.Scale);
        }
    }

    // ───────────────────────────────────────────────────────────────
    //  DRAW MISSILES
    // ───────────────────────────────────────────────────────────────

    private void DrawAllMissiles(DrawingContext dc)
    {
        foreach (var m in _missiles)
        {
            var s = m.Scale;

            if (m.IsBomb)
            {
                // Small dark oval
                var bombBrush = new SolidColorBrush(Color.FromArgb(200, 40, 30, 60));
                bombBrush.Freeze();
                dc.DrawEllipse(bombBrush, null, new Point(m.X, m.Y), 4 * s, 2.5 * s);
            }
            else
            {
                // Missile body — small bright streak
                var missilePen = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 240, 200)), 2 * s);
                missilePen.Freeze();

                // Short line in direction of travel
                var dx = m.TargetAircraft != null ? m.TargetAircraft.X - m.X : (m.GoingRight ? 1 : -1);
                var dy = m.TargetAircraft != null ? m.TargetAircraft.Y - m.Y : 0;
                var dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < 1) dist = 1;
                var len = 10 * s;
                var tailX = m.X - (dx / dist) * len;
                var tailY = m.Y - (dy / dist) * len;

                dc.DrawLine(missilePen, new Point(tailX, tailY), new Point(m.X, m.Y));

                // Engine glow
                var glowBrush = new SolidColorBrush(Color.FromArgb(160, 255, 180, 50));
                glowBrush.Freeze();
                dc.DrawEllipse(glowBrush, null, new Point(tailX, tailY), 3 * s, 3 * s);
            }
        }
    }

    // ───────────────────────────────────────────────────────────────
    //  DRAW INCOMING MISSILES (perspective toward viewer)
    // ───────────────────────────────────────────────────────────────

    private void DrawAllIncomingMissiles(DrawingContext dc)
    {
        foreach (var im in _incomingMissiles)
        {
            var s = im.CurrentScale;
            var t = im.Progress;

            // Missile grows as it approaches — starts as dot, ends as huge shape
            var bodyLen = 5 + s * 3;
            var bodyWidth = 2 + s * 1.5;

            // Bright hot streak
            var alpha = (byte)Math.Min(255, 150 + t * 200);
            var missileBrush = new SolidColorBrush(Color.FromArgb(alpha, 255, 240, 200));
            missileBrush.Freeze();
            dc.DrawEllipse(missileBrush, null, new Point(im.X, im.Y), bodyWidth, bodyLen);

            // Growing engine glow — the "coming at you" effect
            var glowSize = s * 4;
            var glowBrush = new RadialGradientBrush
            {
                GradientStops = new GradientStopCollection
                {
                    new(Color.FromArgb(alpha, 255, 200, 80), 0.0),
                    new(Color.FromArgb((byte)(alpha * 0.6), 255, 120, 30), 0.4),
                    new(Color.FromArgb(0, 255, 60, 10), 1.0),
                }
            };
            glowBrush.Freeze();
            dc.DrawEllipse(glowBrush, null, new Point(im.X, im.Y), glowSize, glowSize);

            // Near the end — screen shake effect via big flash
            if (t > 0.85)
            {
                var flashAlpha = (byte)((t - 0.85) / 0.15 * 120);
                var flashBrush = new RadialGradientBrush
                {
                    GradientStops = new GradientStopCollection
                    {
                        new(Color.FromArgb(flashAlpha, 255, 255, 220), 0.0),
                        new(Color.FromArgb(0, 255, 200, 100), 1.0),
                    }
                };
                flashBrush.Freeze();
                dc.DrawEllipse(flashBrush, null, new Point(im.X, im.Y), glowSize * 3, glowSize * 3);
            }
        }
    }

    // ───────────────────────────────────────────────────────────────
    //  DRAW EXPLOSIONS
    // ───────────────────────────────────────────────────────────────

    private void DrawAllExplosions(DrawingContext dc)
    {
        foreach (var e in _explosions)
        {
            var progress = 1.0 - (e.Life / e.MaxLife);
            var fade = Math.Max(0, e.Life / e.MaxLife);
            var radius = (15 + progress * 40) * e.Scale;

            // Outer glow
            var outerAlpha = (byte)(80 * fade);
            var outerBrush = new RadialGradientBrush
            {
                GradientStops = new GradientStopCollection
                {
                    new(Color.FromArgb(outerAlpha, 255, 160, 30), 0.0),
                    new(Color.FromArgb((byte)(outerAlpha * 0.5), 255, 80, 20), 0.5),
                    new(Color.FromArgb(0, 255, 40, 10), 1.0),
                }
            };
            outerBrush.Freeze();
            dc.DrawEllipse(outerBrush, null, new Point(e.X, e.Y), radius * 2, radius * 2);

            // Inner bright core
            var coreAlpha = (byte)(200 * fade);
            var coreBrush = new RadialGradientBrush
            {
                GradientStops = new GradientStopCollection
                {
                    new(Color.FromArgb(coreAlpha, 255, 255, 200), 0.0),
                    new(Color.FromArgb((byte)(coreAlpha * 0.6), 255, 200, 50), 0.4),
                    new(Color.FromArgb(0, 255, 100, 20), 1.0),
                }
            };
            coreBrush.Freeze();
            dc.DrawEllipse(coreBrush, null, new Point(e.X, e.Y), radius, radius);
        }
    }

    // ───────────────────────────────────────────────────────────────
    //  AIRCRAFT DRAWING
    // ───────────────────────────────────────────────────────────────

    private void DrawAllAircraft(DrawingContext dc)
    {
        var sorted = _aircraft.OrderBy(a => a.Y).ToList();
        foreach (var a in sorted)
        {
            if (a.State == CombatState.Dead) continue;

            // Save transform for rotation (going down spin or dogfight pitch)
            var needsRotation = (a.State == CombatState.GoingDown || a.State == CombatState.Dogfighting)
                                && Math.Abs(a.Rotation) > 0.01;
            if (needsRotation)
            {
                var rotAngle = a.State == CombatState.GoingDown ? a.Rotation * 30 : a.Rotation;
                dc.PushTransform(new RotateTransform(rotAngle, a.X, a.Y));
            }

            // Flickering when hit
            if (a.State == CombatState.Hit && ((int)(_time * 20) % 3 == 0))
            {
                // Flash white briefly
                DrawAircraftFlash(dc, a);
            }
            else
            {
                switch (a.Type)
                {
                    case AircraftType.F16: DrawF16(dc, a); break;
                    case AircraftType.FA18: DrawFA18(dc, a); break;
                    case AircraftType.F15: DrawF15(dc, a); break;
                    case AircraftType.A10: DrawA10(dc, a); break;
                    case AircraftType.F14: DrawF14(dc, a); break;
                    case AircraftType.AH64: DrawAH64(dc, a); break;
                    case AircraftType.Mi28: DrawMi28(dc, a); break;
                    case AircraftType.Hind: DrawHind(dc, a); break;
                    case AircraftType.UH60: DrawUH60(dc, a); break;
                    case AircraftType.UH1: DrawUH1(dc, a); break;
                }
            }

            // Muzzle flash when shooting
            if (a.State == CombatState.Shooting && ((int)(_time * 15) % 2 == 0))
            {
                DrawMuzzleFlash(dc, a);
            }

            // Afterburner — only 25% of jets have them
            if (a.HasAfterburner && a.State != CombatState.Dead)
            {
                var abIntensity = a.State == CombatState.Shooting || a.State == CombatState.Normal ? 1.0 : 0.6;
                abIntensity *= 0.7 + 0.3 * Math.Sin(_time * 25 + a.X * 0.1);
                DrawAfterburner(dc, a, abIntensity);
            }

            if (needsRotation)
            {
                dc.Pop();
            }
        }
    }

    private static void DrawMuzzleFlash(DrawingContext dc, Aircraft a)
    {
        var s = a.Scale;
        var flip = a.GoingRight ? 1.0 : -1.0;
        var noseX = a.X + 42 * s * flip;
        var noseY = a.Y;

        var brush = new RadialGradientBrush
        {
            GradientStops = new GradientStopCollection
            {
                new(Color.FromArgb(200, 255, 255, 180), 0.0),
                new(Color.FromArgb(120, 255, 180, 40), 0.4),
                new(Color.FromArgb(0, 255, 100, 20), 1.0),
            }
        };
        brush.Freeze();
        dc.DrawEllipse(brush, null, new Point(noseX, noseY), 8 * s, 5 * s);
    }

    private static void DrawAircraftFlash(DrawingContext dc, Aircraft a)
    {
        var s = a.Scale;
        var flashBrush = new SolidColorBrush(Color.FromArgb(180, 255, 200, 150));
        flashBrush.Freeze();
        dc.DrawEllipse(flashBrush, null, new Point(a.X, a.Y), 20 * s, 10 * s);
    }

    private static void DrawAfterburner(DrawingContext dc, Aircraft a, double intensity)
    {
        var s = a.Scale;
        var flip = a.GoingRight ? 1.0 : -1.0;

        // Exhaust position (behind the aircraft)
        var exX = a.X - 36 * s * flip;
        var exY = a.Y;

        // Twin engine jets get two burners
        var isTwinEngine = a.Type == AircraftType.FA18 || a.Type == AircraftType.F15 ||
                           a.Type == AircraftType.F14 || a.Type == AircraftType.A10;

        var coneLength = (18 + intensity * 14) * s;
        var coneWidth = (3 + intensity * 2.5) * s;
        var alpha = (byte)(140 * intensity);

        if (isTwinEngine)
        {
            // Two exhaust cones offset vertically
            DrawSingleBurner(dc, exX, exY - 4 * s, coneLength, coneWidth * 0.8, flip, alpha, s);
            DrawSingleBurner(dc, exX, exY + 4 * s, coneLength, coneWidth * 0.8, flip, alpha, s);
        }
        else
        {
            DrawSingleBurner(dc, exX, exY, coneLength, coneWidth, flip, alpha, s);
        }
    }

    private static void DrawSingleBurner(DrawingContext dc, double x, double y,
        double length, double width, double flip, byte alpha, double scale)
    {
        // Inner bright core (blue-white)
        var innerBrush = new RadialGradientBrush
        {
            Center = new Point(0.8 * (flip > 0 ? 1 : 0), 0.5),
            GradientOrigin = new Point(0.8 * (flip > 0 ? 1 : 0), 0.5),
            GradientStops = new GradientStopCollection
            {
                new(Color.FromArgb(alpha, 200, 220, 255), 0.0),
                new(Color.FromArgb((byte)(alpha * 0.8), 100, 160, 255), 0.3),
                new(Color.FromArgb((byte)(alpha * 0.5), 255, 140, 40), 0.7),
                new(Color.FromArgb(0, 255, 80, 20), 1.0),
            }
        };
        innerBrush.Freeze();

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            // Cone shape pointing backward
            var tipX = x - length * flip;
            ctx.BeginFigure(new Point(x, y - width), true, true);
            ctx.QuadraticBezierTo(
                new Point(x - length * 0.5 * flip, y),
                new Point(tipX, y), true, false);
            ctx.QuadraticBezierTo(
                new Point(x - length * 0.5 * flip, y),
                new Point(x, y + width), true, false);
            ctx.LineTo(new Point(x, y - width), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(innerBrush, null, geo);

        // Outer glow
        var glowAlpha = (byte)(alpha * 0.3);
        var glowBrush = new RadialGradientBrush
        {
            GradientStops = new GradientStopCollection
            {
                new(Color.FromArgb(glowAlpha, 100, 150, 255), 0.0),
                new(Color.FromArgb(0, 255, 100, 20), 1.0),
            }
        };
        glowBrush.Freeze();
        dc.DrawEllipse(glowBrush, null, new Point(x - length * 0.3 * flip, y),
            length * 0.6, width * 2.5);
    }

    /// <summary>Draw an F-16 silhouette</summary>
    private static void DrawF16(DrawingContext dc, Aircraft a)
    {
        var s = a.Scale;
        var flip = a.GoingRight ? 1.0 : -1.0;
        var brush = new SolidColorBrush(Color.FromArgb((byte)(140 + s * 80), 20, 12, 40));
        brush.Freeze();

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(a.X + 40 * s * flip, a.Y), true, true);
            ctx.LineTo(new Point(a.X + 25 * s * flip, a.Y - 3 * s), true, false);
            ctx.LineTo(new Point(a.X - 10 * s * flip, a.Y - 3 * s), true, false);
            ctx.LineTo(new Point(a.X - 18 * s * flip, a.Y - 16 * s), true, false);
            ctx.LineTo(new Point(a.X - 25 * s * flip, a.Y - 16 * s), true, false);
            ctx.LineTo(new Point(a.X - 22 * s * flip, a.Y - 3 * s), true, false);
            ctx.LineTo(new Point(a.X - 35 * s * flip, a.Y - 2 * s), true, false);
            ctx.LineTo(new Point(a.X - 38 * s * flip, a.Y + 2 * s), true, false);
            ctx.LineTo(new Point(a.X - 10 * s * flip, a.Y + 3 * s), true, false);
            ctx.LineTo(new Point(a.X - 5 * s * flip, a.Y + 18 * s), true, false);
            ctx.LineTo(new Point(a.X + 8 * s * flip, a.Y + 18 * s), true, false);
            ctx.LineTo(new Point(a.X + 12 * s * flip, a.Y + 3 * s), true, false);
            ctx.LineTo(new Point(a.X + 30 * s * flip, a.Y + 2 * s), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(brush, null, geo);
    }

    /// <summary>Draw an F/A-18 silhouette</summary>
    private static void DrawFA18(DrawingContext dc, Aircraft a)
    {
        var s = a.Scale;
        var flip = a.GoingRight ? 1.0 : -1.0;
        var brush = new SolidColorBrush(Color.FromArgb((byte)(140 + s * 80), 18, 10, 38));
        brush.Freeze();

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(a.X + 38 * s * flip, a.Y), true, true);
            ctx.LineTo(new Point(a.X + 20 * s * flip, a.Y - 3 * s), true, false);
            ctx.LineTo(new Point(a.X - 8 * s * flip, a.Y - 3 * s), true, false);
            ctx.LineTo(new Point(a.X - 14 * s * flip, a.Y - 14 * s), true, false);
            ctx.LineTo(new Point(a.X - 20 * s * flip, a.Y - 14 * s), true, false);
            ctx.LineTo(new Point(a.X - 18 * s * flip, a.Y - 4 * s), true, false);
            ctx.LineTo(new Point(a.X - 22 * s * flip, a.Y - 12 * s), true, false);
            ctx.LineTo(new Point(a.X - 28 * s * flip, a.Y - 12 * s), true, false);
            ctx.LineTo(new Point(a.X - 25 * s * flip, a.Y - 3 * s), true, false);
            ctx.LineTo(new Point(a.X - 36 * s * flip, a.Y), true, false);
            ctx.LineTo(new Point(a.X - 25 * s * flip, a.Y + 3 * s), true, false);
            ctx.LineTo(new Point(a.X - 8 * s * flip, a.Y + 22 * s), true, false);
            ctx.LineTo(new Point(a.X + 5 * s * flip, a.Y + 22 * s), true, false);
            ctx.LineTo(new Point(a.X + 10 * s * flip, a.Y + 3 * s), true, false);
            ctx.LineTo(new Point(a.X + 28 * s * flip, a.Y + 2 * s), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(brush, null, geo);
    }

    /// <summary>Draw an F-15 silhouette</summary>
    private static void DrawF15(DrawingContext dc, Aircraft a)
    {
        var s = a.Scale;
        var flip = a.GoingRight ? 1.0 : -1.0;
        var brush = new SolidColorBrush(Color.FromArgb((byte)(140 + s * 80), 22, 14, 44));
        brush.Freeze();

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(a.X + 42 * s * flip, a.Y), true, true);
            ctx.LineTo(new Point(a.X + 22 * s * flip, a.Y - 4 * s), true, false);
            ctx.LineTo(new Point(a.X - 10 * s * flip, a.Y - 4 * s), true, false);
            ctx.LineTo(new Point(a.X - 16 * s * flip, a.Y - 18 * s), true, false);
            ctx.LineTo(new Point(a.X - 22 * s * flip, a.Y - 18 * s), true, false);
            ctx.LineTo(new Point(a.X - 20 * s * flip, a.Y - 4 * s), true, false);
            ctx.LineTo(new Point(a.X - 24 * s * flip, a.Y - 16 * s), true, false);
            ctx.LineTo(new Point(a.X - 30 * s * flip, a.Y - 16 * s), true, false);
            ctx.LineTo(new Point(a.X - 28 * s * flip, a.Y - 4 * s), true, false);
            ctx.LineTo(new Point(a.X - 40 * s * flip, a.Y), true, false);
            ctx.LineTo(new Point(a.X - 28 * s * flip, a.Y + 4 * s), true, false);
            ctx.LineTo(new Point(a.X - 6 * s * flip, a.Y + 20 * s), true, false);
            ctx.LineTo(new Point(a.X + 8 * s * flip, a.Y + 20 * s), true, false);
            ctx.LineTo(new Point(a.X + 14 * s * flip, a.Y + 4 * s), true, false);
            ctx.LineTo(new Point(a.X + 30 * s * flip, a.Y + 2 * s), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(brush, null, geo);
    }

    /// <summary>Draw an A-10 silhouette</summary>
    private static void DrawA10(DrawingContext dc, Aircraft a)
    {
        var s = a.Scale;
        var flip = a.GoingRight ? 1.0 : -1.0;
        var brush = new SolidColorBrush(Color.FromArgb((byte)(140 + s * 80), 16, 10, 34));
        brush.Freeze();

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(a.X + 48 * s * flip, a.Y + 2 * s), true, true);
            ctx.LineTo(new Point(a.X + 30 * s * flip, a.Y - 3 * s), true, false);
            ctx.LineTo(new Point(a.X - 10 * s * flip, a.Y - 3 * s), true, false);
            ctx.LineTo(new Point(a.X - 20 * s * flip, a.Y - 15 * s), true, false);
            ctx.LineTo(new Point(a.X - 26 * s * flip, a.Y - 15 * s), true, false);
            ctx.LineTo(new Point(a.X - 24 * s * flip, a.Y - 3 * s), true, false);
            ctx.LineTo(new Point(a.X - 28 * s * flip, a.Y - 13 * s), true, false);
            ctx.LineTo(new Point(a.X - 34 * s * flip, a.Y - 13 * s), true, false);
            ctx.LineTo(new Point(a.X - 32 * s * flip, a.Y - 3 * s), true, false);
            ctx.LineTo(new Point(a.X - 38 * s * flip, a.Y), true, false);
            ctx.LineTo(new Point(a.X - 32 * s * flip, a.Y + 3 * s), true, false);
            ctx.LineTo(new Point(a.X - 10 * s * flip, a.Y + 25 * s), true, false);
            ctx.LineTo(new Point(a.X - 14 * s * flip, a.Y + 25 * s), true, false);
            ctx.LineTo(new Point(a.X - 18 * s * flip, a.Y + 20 * s), true, false);
            ctx.LineTo(new Point(a.X - 18 * s * flip, a.Y + 12 * s), true, false);
            ctx.LineTo(new Point(a.X - 14 * s * flip, a.Y + 8 * s), true, false);
            ctx.LineTo(new Point(a.X - 6 * s * flip, a.Y + 25 * s), true, false);
            ctx.LineTo(new Point(a.X + 6 * s * flip, a.Y + 25 * s), true, false);
            ctx.LineTo(new Point(a.X + 12 * s * flip, a.Y + 3 * s), true, false);
            ctx.LineTo(new Point(a.X + 32 * s * flip, a.Y + 3 * s), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(brush, null, geo);
    }

    /// <summary>Draw an F-14 silhouette</summary>
    private static void DrawF14(DrawingContext dc, Aircraft a)
    {
        var s = a.Scale;
        var flip = a.GoingRight ? 1.0 : -1.0;
        var brush = new SolidColorBrush(Color.FromArgb((byte)(140 + s * 80), 24, 14, 42));
        brush.Freeze();

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(a.X + 45 * s * flip, a.Y), true, true);
            ctx.LineTo(new Point(a.X + 25 * s * flip, a.Y - 4 * s), true, false);
            ctx.LineTo(new Point(a.X - 5 * s * flip, a.Y - 5 * s), true, false);
            ctx.LineTo(new Point(a.X - 12 * s * flip, a.Y - 16 * s), true, false);
            ctx.LineTo(new Point(a.X - 18 * s * flip, a.Y - 16 * s), true, false);
            ctx.LineTo(new Point(a.X - 16 * s * flip, a.Y - 5 * s), true, false);
            ctx.LineTo(new Point(a.X - 26 * s * flip, a.Y - 14 * s), true, false);
            ctx.LineTo(new Point(a.X - 32 * s * flip, a.Y - 14 * s), true, false);
            ctx.LineTo(new Point(a.X - 30 * s * flip, a.Y - 4 * s), true, false);
            ctx.LineTo(new Point(a.X - 42 * s * flip, a.Y), true, false);
            ctx.LineTo(new Point(a.X - 30 * s * flip, a.Y + 4 * s), true, false);
            ctx.LineTo(new Point(a.X - 4 * s * flip, a.Y + 18 * s), true, false);
            ctx.LineTo(new Point(a.X + 10 * s * flip, a.Y + 14 * s), true, false);
            ctx.LineTo(new Point(a.X + 14 * s * flip, a.Y + 4 * s), true, false);
            ctx.LineTo(new Point(a.X + 32 * s * flip, a.Y + 3 * s), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(brush, null, geo);
    }

    // ───────────────────────────────────────────────────────────────
    //  HELICOPTER SILHOUETTES
    // ───────────────────────────────────────────────────────────────

    private void DrawRotor(DrawingContext dc, Aircraft a, double hubX, double hubY)
    {
        var s = a.Scale;
        // Spinning rotor — appears as a line that rotates
        var rotorLen = 35 * s;
        var angle = _time * 18 + a.X * 0.5; // fast spin
        var rx = Math.Cos(angle) * rotorLen;
        var ry = Math.Sin(angle) * rotorLen * 0.15; // perspective flattening

        var rotorPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(100 + s * 60), 30, 20, 55)), 1.2 * s);
        rotorPen.Freeze();
        dc.DrawLine(rotorPen, new Point(hubX - rx, hubY - ry), new Point(hubX + rx, hubY + ry));

        // Second blade perpendicular
        var rx2 = Math.Cos(angle + Math.PI / 2) * rotorLen;
        var ry2 = Math.Sin(angle + Math.PI / 2) * rotorLen * 0.15;
        dc.DrawLine(rotorPen, new Point(hubX - rx2, hubY - ry2), new Point(hubX + rx2, hubY + ry2));

        // Rotor disc shimmer
        var discBrush = new SolidColorBrush(Color.FromArgb((byte)(25 + s * 15), 60, 40, 90));
        discBrush.Freeze();
        dc.DrawEllipse(discBrush, null, new Point(hubX, hubY), rotorLen, rotorLen * 0.15);
    }

    private void DrawTailRotor(DrawingContext dc, double x, double y, double s)
    {
        var angle = _time * 30;
        var len = 6 * s;
        var rx = Math.Cos(angle) * len;
        var ry = Math.Sin(angle) * len;
        var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(80 + s * 40), 30, 20, 50)), 0.8 * s);
        pen.Freeze();
        dc.DrawLine(pen, new Point(x - rx, y - ry), new Point(x + rx, y + ry));
    }

    /// <summary>AH-64 Apache — attack helicopter, stub wings, chin turret</summary>
    private void DrawAH64(DrawingContext dc, Aircraft a)
    {
        var s = a.Scale;
        var flip = a.GoingRight ? 1.0 : -1.0;
        var brush = new SolidColorBrush(Color.FromArgb((byte)(140 + s * 80), 18, 12, 35));
        brush.Freeze();

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            // Nose with chin turret
            ctx.BeginFigure(new Point(a.X + 22 * s * flip, a.Y + 6 * s), true, true);
            ctx.LineTo(new Point(a.X + 18 * s * flip, a.Y + 2 * s), true, false);
            ctx.LineTo(new Point(a.X + 10 * s * flip, a.Y - 2 * s), true, false);
            // Canopy
            ctx.LineTo(new Point(a.X + 5 * s * flip, a.Y - 6 * s), true, false);
            ctx.LineTo(new Point(a.X - 5 * s * flip, a.Y - 7 * s), true, false);
            // Rotor hub area
            ctx.LineTo(new Point(a.X - 2 * s * flip, a.Y - 10 * s), true, false);
            ctx.LineTo(new Point(a.X + 2 * s * flip, a.Y - 10 * s), true, false);
            ctx.LineTo(new Point(a.X - 8 * s * flip, a.Y - 7 * s), true, false);
            // Tail boom
            ctx.LineTo(new Point(a.X - 28 * s * flip, a.Y - 5 * s), true, false);
            // Tail fin
            ctx.LineTo(new Point(a.X - 32 * s * flip, a.Y - 12 * s), true, false);
            ctx.LineTo(new Point(a.X - 35 * s * flip, a.Y - 12 * s), true, false);
            ctx.LineTo(new Point(a.X - 33 * s * flip, a.Y - 4 * s), true, false);
            // Bottom tail
            ctx.LineTo(new Point(a.X - 30 * s * flip, a.Y), true, false);
            // Belly
            ctx.LineTo(new Point(a.X - 8 * s * flip, a.Y + 2 * s), true, false);
            // Stub wings
            ctx.LineTo(new Point(a.X - 6 * s * flip, a.Y + 10 * s), true, false);
            ctx.LineTo(new Point(a.X + 2 * s * flip, a.Y + 10 * s), true, false);
            ctx.LineTo(new Point(a.X + 4 * s * flip, a.Y + 3 * s), true, false);
            // Landing gear area
            ctx.LineTo(new Point(a.X + 14 * s * flip, a.Y + 4 * s), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(brush, null, geo);

        DrawRotor(dc, a, a.X, a.Y - 10 * s);
        DrawTailRotor(dc, a.X - 33 * s * flip, a.Y - 8 * s, s);
    }

    /// <summary>Mi-28 Havoc — Russian attack helo, similar profile to Apache</summary>
    private void DrawMi28(DrawingContext dc, Aircraft a)
    {
        var s = a.Scale;
        var flip = a.GoingRight ? 1.0 : -1.0;
        var brush = new SolidColorBrush(Color.FromArgb((byte)(140 + s * 80), 20, 14, 38));
        brush.Freeze();

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            // Rounded nose
            ctx.BeginFigure(new Point(a.X + 20 * s * flip, a.Y + 4 * s), true, true);
            ctx.LineTo(new Point(a.X + 16 * s * flip, a.Y), true, false);
            ctx.LineTo(new Point(a.X + 8 * s * flip, a.Y - 4 * s), true, false);
            // Canopy bulge
            ctx.LineTo(new Point(a.X + 2 * s * flip, a.Y - 8 * s), true, false);
            ctx.LineTo(new Point(a.X - 4 * s * flip, a.Y - 9 * s), true, false);
            // Hub
            ctx.LineTo(new Point(a.X - 1 * s * flip, a.Y - 12 * s), true, false);
            ctx.LineTo(new Point(a.X + 3 * s * flip, a.Y - 12 * s), true, false);
            ctx.LineTo(new Point(a.X - 6 * s * flip, a.Y - 8 * s), true, false);
            // Tail boom (thicker than Apache)
            ctx.LineTo(new Point(a.X - 26 * s * flip, a.Y - 6 * s), true, false);
            ctx.LineTo(new Point(a.X - 30 * s * flip, a.Y - 14 * s), true, false);
            ctx.LineTo(new Point(a.X - 34 * s * flip, a.Y - 14 * s), true, false);
            ctx.LineTo(new Point(a.X - 32 * s * flip, a.Y - 5 * s), true, false);
            ctx.LineTo(new Point(a.X - 28 * s * flip, a.Y + 1 * s), true, false);
            // Belly
            ctx.LineTo(new Point(a.X - 6 * s * flip, a.Y + 3 * s), true, false);
            // Stub wings with stores
            ctx.LineTo(new Point(a.X - 4 * s * flip, a.Y + 12 * s), true, false);
            ctx.LineTo(new Point(a.X + 4 * s * flip, a.Y + 12 * s), true, false);
            ctx.LineTo(new Point(a.X + 6 * s * flip, a.Y + 3 * s), true, false);
            ctx.LineTo(new Point(a.X + 14 * s * flip, a.Y + 5 * s), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(brush, null, geo);

        DrawRotor(dc, a, a.X + 1 * s * flip, a.Y - 12 * s);
        DrawTailRotor(dc, a.X - 32 * s * flip, a.Y - 9 * s, s);
    }

    /// <summary>Mi-24 Hind — big transport/attack helo, distinctive profile</summary>
    private void DrawHind(DrawingContext dc, Aircraft a)
    {
        var s = a.Scale;
        var flip = a.GoingRight ? 1.0 : -1.0;
        var brush = new SolidColorBrush(Color.FromArgb((byte)(140 + s * 80), 22, 16, 40));
        brush.Freeze();

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            // Distinctive sloped nose
            ctx.BeginFigure(new Point(a.X + 24 * s * flip, a.Y + 5 * s), true, true);
            ctx.LineTo(new Point(a.X + 20 * s * flip, a.Y), true, false);
            ctx.LineTo(new Point(a.X + 12 * s * flip, a.Y - 5 * s), true, false);
            // Big greenhouse canopy
            ctx.LineTo(new Point(a.X + 4 * s * flip, a.Y - 10 * s), true, false);
            ctx.LineTo(new Point(a.X - 6 * s * flip, a.Y - 12 * s), true, false);
            // Hub
            ctx.LineTo(new Point(a.X - 3 * s * flip, a.Y - 15 * s), true, false);
            ctx.LineTo(new Point(a.X + 1 * s * flip, a.Y - 15 * s), true, false);
            ctx.LineTo(new Point(a.X - 10 * s * flip, a.Y - 11 * s), true, false);
            // Thicker fuselage (troop cabin)
            ctx.LineTo(new Point(a.X - 22 * s * flip, a.Y - 8 * s), true, false);
            // Tail boom rises
            ctx.LineTo(new Point(a.X - 32 * s * flip, a.Y - 14 * s), true, false);
            ctx.LineTo(new Point(a.X - 36 * s * flip, a.Y - 14 * s), true, false);
            ctx.LineTo(new Point(a.X - 34 * s * flip, a.Y - 6 * s), true, false);
            ctx.LineTo(new Point(a.X - 30 * s * flip, a.Y), true, false);
            // Fat belly
            ctx.LineTo(new Point(a.X - 10 * s * flip, a.Y + 4 * s), true, false);
            // Retractable gear bumps
            ctx.LineTo(new Point(a.X - 5 * s * flip, a.Y + 8 * s), true, false);
            ctx.LineTo(new Point(a.X + 5 * s * flip, a.Y + 8 * s), true, false);
            ctx.LineTo(new Point(a.X + 8 * s * flip, a.Y + 4 * s), true, false);
            // Stub wings
            ctx.LineTo(new Point(a.X + 2 * s * flip, a.Y + 14 * s), true, false);
            ctx.LineTo(new Point(a.X + 10 * s * flip, a.Y + 14 * s), true, false);
            ctx.LineTo(new Point(a.X + 12 * s * flip, a.Y + 5 * s), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(brush, null, geo);

        DrawRotor(dc, a, a.X - 1 * s * flip, a.Y - 15 * s);
        DrawTailRotor(dc, a.X - 35 * s * flip, a.Y - 10 * s, s);
    }

    /// <summary>UH-60 Black Hawk — utility helo, T-tail</summary>
    private void DrawUH60(DrawingContext dc, Aircraft a)
    {
        var s = a.Scale;
        var flip = a.GoingRight ? 1.0 : -1.0;
        var brush = new SolidColorBrush(Color.FromArgb((byte)(140 + s * 80), 16, 10, 32));
        brush.Freeze();

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            // Rounded nose
            ctx.BeginFigure(new Point(a.X + 18 * s * flip, a.Y + 3 * s), true, true);
            ctx.LineTo(new Point(a.X + 14 * s * flip, a.Y - 2 * s), true, false);
            ctx.LineTo(new Point(a.X + 6 * s * flip, a.Y - 6 * s), true, false);
            // Cabin roof
            ctx.LineTo(new Point(a.X - 4 * s * flip, a.Y - 8 * s), true, false);
            // Hub
            ctx.LineTo(new Point(a.X - 1 * s * flip, a.Y - 11 * s), true, false);
            ctx.LineTo(new Point(a.X + 3 * s * flip, a.Y - 11 * s), true, false);
            ctx.LineTo(new Point(a.X - 8 * s * flip, a.Y - 7 * s), true, false);
            // Tail boom tapering up
            ctx.LineTo(new Point(a.X - 28 * s * flip, a.Y - 10 * s), true, false);
            // T-tail
            ctx.LineTo(new Point(a.X - 30 * s * flip, a.Y - 16 * s), true, false);
            ctx.LineTo(new Point(a.X - 34 * s * flip, a.Y - 16 * s), true, false);
            ctx.LineTo(new Point(a.X - 32 * s * flip, a.Y - 9 * s), true, false);
            ctx.LineTo(new Point(a.X - 30 * s * flip, a.Y - 3 * s), true, false);
            // Belly
            ctx.LineTo(new Point(a.X - 6 * s * flip, a.Y + 4 * s), true, false);
            // Landing gear
            ctx.LineTo(new Point(a.X - 4 * s * flip, a.Y + 8 * s), true, false);
            ctx.LineTo(new Point(a.X + 8 * s * flip, a.Y + 8 * s), true, false);
            ctx.LineTo(new Point(a.X + 10 * s * flip, a.Y + 4 * s), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(brush, null, geo);

        DrawRotor(dc, a, a.X + 1 * s * flip, a.Y - 11 * s);
        DrawTailRotor(dc, a.X - 32 * s * flip, a.Y - 12 * s, s);
    }

    /// <summary>UH-1 Huey — classic single rotor, skid landing gear</summary>
    private void DrawUH1(DrawingContext dc, Aircraft a)
    {
        var s = a.Scale;
        var flip = a.GoingRight ? 1.0 : -1.0;
        var brush = new SolidColorBrush(Color.FromArgb((byte)(140 + s * 80), 14, 10, 30));
        brush.Freeze();

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            // Bubble nose
            ctx.BeginFigure(new Point(a.X + 16 * s * flip, a.Y + 5 * s), true, true);
            ctx.LineTo(new Point(a.X + 14 * s * flip, a.Y), true, false);
            ctx.LineTo(new Point(a.X + 8 * s * flip, a.Y - 4 * s), true, false);
            // Cabin
            ctx.LineTo(new Point(a.X - 2 * s * flip, a.Y - 7 * s), true, false);
            // Hub mast
            ctx.LineTo(new Point(a.X + 1 * s * flip, a.Y - 11 * s), true, false);
            ctx.LineTo(new Point(a.X + 4 * s * flip, a.Y - 11 * s), true, false);
            ctx.LineTo(new Point(a.X - 6 * s * flip, a.Y - 6 * s), true, false);
            // Slim tail boom
            ctx.LineTo(new Point(a.X - 28 * s * flip, a.Y - 5 * s), true, false);
            // Small vertical fin
            ctx.LineTo(new Point(a.X - 30 * s * flip, a.Y - 10 * s), true, false);
            ctx.LineTo(new Point(a.X - 33 * s * flip, a.Y - 10 * s), true, false);
            ctx.LineTo(new Point(a.X - 31 * s * flip, a.Y - 4 * s), true, false);
            ctx.LineTo(new Point(a.X - 29 * s * flip, a.Y), true, false);
            // Belly
            ctx.LineTo(new Point(a.X - 4 * s * flip, a.Y + 3 * s), true, false);
            // Skid landing gear
            ctx.LineTo(new Point(a.X - 8 * s * flip, a.Y + 5 * s), true, false);
            ctx.LineTo(new Point(a.X - 10 * s * flip, a.Y + 10 * s), true, false);
            ctx.LineTo(new Point(a.X + 12 * s * flip, a.Y + 10 * s), true, false);
            ctx.LineTo(new Point(a.X + 10 * s * flip, a.Y + 5 * s), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(brush, null, geo);

        DrawRotor(dc, a, a.X + 2.5 * s * flip, a.Y - 11 * s);
        DrawTailRotor(dc, a.X - 31 * s * flip, a.Y - 7 * s, s);
    }

    // ───────────────────────────────────────────────────────────────
    //  FADE OVERLAY (so content is readable)
    // ───────────────────────────────────────────────────────────────

    private static void DrawFade(DrawingContext dc, double w, double h)
    {
        var fade = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1),
            GradientStops = new GradientStopCollection
            {
                new(Color.FromArgb(190, 11, 10, 26), 0.0),
                new(Color.FromArgb(130, 11, 10, 26), 0.25),
                new(Color.FromArgb(60, 11, 10, 26), 0.55),
                new(Color.FromArgb(20, 11, 10, 26), 0.75),
                new(Color.FromArgb(0, 11, 10, 26), 1.0),
            }
        };
        fade.Freeze();
        dc.DrawRectangle(fade, null, new Rect(0, 0, w, h));
    }
}

// ───────────────────────────────────────────────────────────────
//  MODELS
// ───────────────────────────────────────────────────────────────

internal enum AircraftType { F16, FA18, F15, A10, F14, AH64, Mi28, Hind, UH60, UH1 }

internal enum CombatState { Normal, Shooting, Hit, Smoking, GoingDown, Dead, Dogfighting }

internal class Aircraft
{
    public AircraftType Type { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public double OriginalY { get; set; }
    public double Speed { get; init; }
    public double Scale { get; init; }
    public bool GoingRight { get; init; }
    public CombatState State { get; set; }
    public double ShootCooldown { get; set; }
    public double ShootTimer { get; set; }
    public double BurstTimer { get; set; }
    public int BurstCount { get; set; }
    public Aircraft? Target { get; set; }
    public double Health { get; set; } = 1.0;
    public double HitTimer { get; set; }
    public double SmokeTimer { get; set; }
    public double FallSpeed { get; set; }
    public double SpinRate { get; set; }
    public double Rotation { get; set; }
    public double MissileCooldown { get; set; }
    public bool HasAfterburner { get; set; }
    public bool IsHelicopter { get; set; }
    // Dogfight loop
    public double DogfightTimer { get; set; }
    public double DogfightDuration { get; set; }
    public double LoopPhase { get; set; }
    public double LoopAmplitude { get; set; }
}

internal class Missile
{
    public double X { get; set; }
    public double Y { get; set; }
    public Aircraft? TargetAircraft { get; set; }
    public double TargetGroundX { get; set; }
    public double TargetGroundY { get; set; }
    public double Speed { get; set; }
    public double Scale { get; set; }
    public double Life { get; set; }
    public bool IsBomb { get; set; }
    public double VY { get; set; }
    public bool GoingRight { get; set; }
}

internal class IncomingMissile
{
    public double X { get; set; }
    public double Y { get; set; }
    public double StartX { get; set; }
    public double StartY { get; set; }
    public double TargetX { get; set; }
    public double TargetY { get; set; }
    public double Progress { get; set; }
    public double Duration { get; set; }
    public double Scale { get; set; }
    public double CurrentScale { get; set; }
}

internal class Tracer
{
    public double X { get; set; }
    public double Y { get; set; }
    public double VX { get; set; }
    public double VY { get; set; }
    public double Life { get; set; }
    public double Scale { get; set; }
}

internal class Explosion
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Scale { get; set; }
    public double Life { get; set; }
    public double MaxLife { get; set; }
}

internal class SmokeParticle
{
    public double X { get; set; }
    public double Y { get; set; }
    public double VX { get; set; }
    public double VY { get; set; }
    public double Life { get; set; }
    public double MaxLife { get; set; }
    public double Size { get; set; }
    public bool IsFire { get; set; }
}

internal class SamSite
{
    public double X { get; set; }
    public double Y { get; set; }
    public double RadarAngle { get; set; }
    public double RadarSpeed { get; set; } = 90; // degrees per second
    public double FireCooldown { get; set; } = 10;
    public double MuzzleFlash { get; set; }
}
