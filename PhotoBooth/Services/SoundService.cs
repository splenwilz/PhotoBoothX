using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Photobooth.Services
{
    /// <summary>
    /// Service for playing sound effects in the photobooth application
    /// </summary>
    public class SoundService
    {
        private static readonly Lazy<SoundService> _instance = new Lazy<SoundService>(() => new SoundService());
        public static SoundService Instance => _instance.Value;

        private MediaPlayer? _shutterMediaPlayer;
        private MediaPlayer? _countdownMediaPlayer;
        private MediaPlayer? _successMediaPlayer;
        private string? _shutterSoundPath;

        private SoundService()
        {
            InitializeSounds();
        }

        /// <summary>
        /// Initialize sound players with embedded or system sounds
        /// </summary>
        private void InitializeSounds()
        {
            try
            {
                // Try to load custom sound files first, fall back to system sounds
                LoadCustomSounds();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SoundService: Failed to load custom sounds, using system sounds: {ex.Message}");
                LoadSystemSounds();
            }
        }

        /// <summary>
        /// Load custom sound files if available
        /// </summary>
        private void LoadCustomSounds()
        {
            var appPath = AppDomain.CurrentDomain.BaseDirectory;
            var soundsPath = Path.Combine(appPath, "Assets", "Sounds");
            Console.WriteLine($"SoundService: Looking for sounds in: {soundsPath}");

            // Camera shutter sound - check for multiple formats
            var shutterPaths = new[]
            {
                Path.Combine(soundsPath, "camera-13695.mp3"),  // Your specific file
                Path.Combine(soundsPath, "camera_shutter.mp3"),
                Path.Combine(soundsPath, "camera_shutter.wav")
            };

            foreach (var shutterPath in shutterPaths)
            {
                Console.WriteLine($"SoundService: Checking for file: {shutterPath}");
                if (File.Exists(shutterPath))
                {
                    try
                    {
                        // Initialize MediaPlayer on UI thread
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _shutterMediaPlayer = new MediaPlayer();
                            _shutterMediaPlayer.Open(new Uri(shutterPath, UriKind.Absolute));
                            _shutterSoundPath = shutterPath;
                        });
                        Console.WriteLine($"SoundService: Successfully loaded custom camera shutter sound: {Path.GetFileName(shutterPath)}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"SoundService: Failed to load {shutterPath}: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"SoundService: File not found: {shutterPath}");
                }
            }

            // Countdown beep sound
            var countdownPaths = new[]
            {
                Path.Combine(soundsPath, "countdown_beep.mp3"),
                Path.Combine(soundsPath, "countdown_beep.wav")
            };

            foreach (var countdownPath in countdownPaths)
            {
                if (File.Exists(countdownPath))
                {
                    try
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _countdownMediaPlayer = new MediaPlayer();
                            _countdownMediaPlayer.Open(new Uri(countdownPath, UriKind.Absolute));
                        });
                        Console.WriteLine($"SoundService: Loaded custom countdown sound: {Path.GetFileName(countdownPath)}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"SoundService: Failed to load countdown sound {countdownPath}: {ex.Message}");
                    }
                }
            }

            // Success/completion sound
            var successPaths = new[]
            {
                Path.Combine(soundsPath, "success.mp3"),
                Path.Combine(soundsPath, "success.wav")
            };

            foreach (var successPath in successPaths)
            {
                if (File.Exists(successPath))
                {
                    try
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _successMediaPlayer = new MediaPlayer();
                            _successMediaPlayer.Open(new Uri(successPath, UriKind.Absolute));
                        });
                        Console.WriteLine($"SoundService: Loaded custom success sound: {Path.GetFileName(successPath)}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"SoundService: Failed to load success sound {successPath}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Load system sounds as fallback
        /// </summary>
        private void LoadSystemSounds()
        {
            // Use system sounds as fallback
            Console.WriteLine("SoundService: Using system sounds as fallback");
        }

        /// <summary>
        /// Play camera shutter sound
        /// </summary>
        public void PlayCameraShutter()
        {
            try
            {
                if (_shutterMediaPlayer != null)
                {
                    Console.WriteLine($"SoundService: Playing custom camera shutter sound from: {_shutterSoundPath}");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _shutterMediaPlayer.Position = TimeSpan.Zero; // Reset to beginning
                        _shutterMediaPlayer.Play();
                    });
                }
                else
                {
                    Console.WriteLine("SoundService: No custom shutter sound loaded, using programmatic sound");
                    // Generate a camera shutter-like sound programmatically
                    Task.Run(() => PlayProgrammaticShutterSound());
                }
                
                Console.WriteLine("SoundService: Camera shutter sound played");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SoundService: Failed to play camera shutter sound: {ex.Message}");
                // Fallback to system sound
                Task.Run(() => SystemSounds.Exclamation.Play());
            }
        }

        /// <summary>
        /// Generate a programmatic camera shutter sound
        /// </summary>
        private void PlayProgrammaticShutterSound()
        {
            try
            {
                // Create a quick sequence of beeps to simulate shutter sound
                // High frequency click followed by lower frequency
                Console.Beep(800, 50);   // Quick high click
                System.Threading.Thread.Sleep(10);
                Console.Beep(400, 100);  // Lower mechanical sound
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SoundService: Failed to generate programmatic shutter sound: {ex.Message}");
                // Ultimate fallback
                SystemSounds.Exclamation.Play();
            }
        }

        /// <summary>
        /// Play countdown beep sound - currently silent, custom sounds may be added in future
        /// </summary>
        public void PlayCountdownBeep()
        {
            try
            {
                if (_countdownMediaPlayer != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _countdownMediaPlayer.Position = TimeSpan.Zero;
                        _countdownMediaPlayer.Play();
                    });
                    Console.WriteLine("SoundService: Custom countdown sound played");
                }
                else
                {
                    // Silent countdown - no sound for now
                    Console.WriteLine("SoundService: Countdown silent (no custom sound configured)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SoundService: Failed to play countdown beep: {ex.Message}");
            }
        }

        /// <summary>
        /// Play success/completion sound
        /// </summary>
        public void PlaySuccess()
        {
            try
            {
                if (_successMediaPlayer != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _successMediaPlayer.Position = TimeSpan.Zero;
                        _successMediaPlayer.Play();
                    });
                }
                else
                {
                    // Use system sound as fallback
                    Task.Run(() => SystemSounds.Asterisk.Play());
                }
                
                Console.WriteLine("SoundService: Success sound played");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SoundService: Failed to play success sound: {ex.Message}");
            }
        }

        /// <summary>
        /// Play error sound
        /// </summary>
        public void PlayError()
        {
            try
            {
                Task.Run(() => SystemSounds.Hand.Play());
                Console.WriteLine("SoundService: Error sound played");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SoundService: Failed to play error sound: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop all currently playing sounds
        /// </summary>
        public void StopAllSounds()
        {
            try
            {
                if (Application.Current != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _shutterMediaPlayer?.Stop();
                        _countdownMediaPlayer?.Stop();
                        _successMediaPlayer?.Stop();
                    });
                }
                Console.WriteLine("SoundService: All sounds stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SoundService: Failed to stop sounds: {ex.Message}");
            }
        }

        /// <summary>
        /// Dispose of sound resources
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (Application.Current != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _shutterMediaPlayer?.Close();
                        _countdownMediaPlayer?.Close();
                        _successMediaPlayer?.Close();
                    });
                }
                Console.WriteLine("SoundService: Sound resources disposed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SoundService: Failed to dispose sound resources: {ex.Message}");
            }
        }
    }
} 