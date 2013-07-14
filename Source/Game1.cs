using System.IO;
using System.Threading;
using EasyStorage;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using System.Diagnostics;
using ResolutionBuddy;
using ToastBuddyLib;
using FontBuddyLib;

namespace EasyStorageSample
{
	public class Game1 : Game
	{
		GraphicsDeviceManager graphics;
		SpriteBatch spriteBatch;

		ToastBuddy m_Messages;
		
		IAsyncSaveDevice saveDevice;
		
		GamePadState gps, gpsPrev;
		KeyboardState ks, ksPrev;

		FontBuddy InstructionFont;

		//A number we are going to write out to the file.
		int iNum = 0;
		
		public Game1()
		{
			graphics = new GraphicsDeviceManager(this);
			graphics.SupportedOrientations = DisplayOrientation.LandscapeLeft;
			Resolution.Init(ref graphics);
			Content.RootDirectory = "Content";

			Resolution.SetDesiredResolution(1280, 720);
			Resolution.SetScreenResolution(1280, 720, true);

			m_Messages = new ToastBuddy(this, "ArialBlack24", UpperRight, Resolution.TransformationMatrix);
			Components.Add(m_Messages);
		}

		public Vector2 UpperRight()
		{
			return new Vector2(Resolution.TitleSafeArea.Right, Resolution.TitleSafeArea.Top);
		}
		
		protected override void Initialize()
		{
			// we can set our supported languages explicitly or we can allow the
			// game to support all the languages. the first language given will
			// be the default if the current language is not one of the supported
			// languages. this only affects the text found in message boxes shown
			// by EasyStorage and does not have any affect on the rest of the game.
			EasyStorageSettings.SetSupportedLanguages(Language.French, Language.Spanish);
			
			// on Windows Phone we use a save device that uses IsolatedStorage
			// on Windows and Xbox 360, we use a save device that gets a shared StorageDevice to handle our file IO.
			#if WINDOWS_PHONE || ANDROID
			saveDevice = new IsolatedStorageSaveDevice();
			#else
			// create and add our SaveDevice
			SharedSaveDevice sharedSaveDevice = new SharedSaveDevice();
			Components.Add(sharedSaveDevice);
			
			// make sure we hold on to the device
			saveDevice = sharedSaveDevice;
			
			// hook two event handlers to force the user to choose a new device if they cancel the
			// device selector or if they disconnect the storage device after selecting it
			sharedSaveDevice.DeviceSelectorCanceled += (s, e) => e.Response = SaveDeviceEventResponse.Force;
			sharedSaveDevice.DeviceDisconnected += (s, e) => e.Response = SaveDeviceEventResponse.Force;
			
			// prompt for a device on the first Update we can
			sharedSaveDevice.PromptForDevice();
			#endif
			
			// we use the tap gesture for input on the phone
			TouchPanel.EnabledGestures = GestureType.Tap;
			
			#if XBOX
			// add the GamerServicesComponent
			Components.Add(new Microsoft.Xna.Framework.GamerServices.GamerServicesComponent(this));
			#endif
			
			// hook an event so we can see that it does fire
			saveDevice.SaveCompleted += new SaveCompletedEventHandler(saveDevice_SaveCompleted);
			
			base.Initialize();
		}
		
		void saveDevice_SaveCompleted(object sender, FileActionCompletedEventArgs args)
		{
			string strText = "Save completed.";
			if (null != args.Error)
			{
				strText = args.Error.Message;
			}

			// just write some debug output for our verification
			m_Messages.ShowFormattedMessage(strText);
		}
		
		protected override void LoadContent()
		{
			spriteBatch = new SpriteBatch(GraphicsDevice);
			InstructionFont = new FontBuddy();
			InstructionFont.Font = Content.Load<SpriteFont>("ArialBlack24");
		}
		
		protected override void Update(GameTime gameTime)
		{
			// For Mobile devices, this logic will close the Game when the Back button is pressed
			if ((GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed) ||
			    (Keyboard.GetState(PlayerIndex.One).IsKeyDown(Keys.Escape)))
			{
				Exit();
			}

			gpsPrev = gps;
			ksPrev = ks;
			gps = GamePad.GetState(PlayerIndex.One);
			ks = Keyboard.GetState();
			
			bool tapped = false;
			while (TouchPanel.IsGestureAvailable)
			{
				GestureSample gesture = TouchPanel.ReadGesture();
				if (gesture.GestureType == GestureType.Tap)
					tapped = true;
			}
			
			if ((gps.IsButtonDown(Buttons.A) && gpsPrev.IsButtonUp(Buttons.A)) ||
			    (ks.IsKeyDown(Keys.Z) && ksPrev.IsKeyUp(Keys.Z)) ||
			    tapped)
			{
				WriteStuff();
			}
			else if ((gps.IsButtonDown(Buttons.B) && gpsPrev.IsButtonUp(Buttons.B)) ||
			         (ks.IsKeyDown(Keys.X) && ksPrev.IsKeyUp(Keys.X)) ||
			         tapped)
			{
				ReadStuff();
			}
			
			base.Update(gameTime);
		}

		private void WriteStuff()
		{
			// make sure the device is ready
			if (saveDevice.IsReady)
			{
				// save a file asynchronously. this will trigger IsBusy to return true
				// for the duration of the save process.
				saveDevice.SaveAsync(
					"TestContainer",
					"MyFile.txt",
					stream =>
					{
					// simulate a really, really long save operation so we can visually see that
					// IsBusy stays true while we're saving
					Thread.Sleep(1000);

					using (StreamWriter writer = new StreamWriter(stream))
					{
						//write out to the file
						string message = string.Format("Hello, World {0}!", iNum++);
						writer.WriteLine(message);

						//pop up a message to tell the user what we wrote
						m_Messages.ShowFormattedMessage("Wrote: \"{0}\"", message);
					}
				});
			}
		}

		private void ReadStuff()
		{
			//if there is a file there, load it into the system
			if (saveDevice.FileExists("TestContainer", "MyFile.txt"))
			{
				saveDevice.Load(
					"TestContainer",
					"MyFile.txt",
					stream => 
					{
					using (StreamReader reader = new StreamReader(stream))
					{
						string messageText = reader.ReadLine();

						//pop up a message to tell the user what we wrote
						m_Messages.ShowFormattedMessage("Read: \"{0}\"", messageText);
					}
				});

				m_Messages.ShowMessage("Finished reading file.");
			}
			else
			{
				m_Messages.ShowMessage("No file :(");
			}
		}
		
		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice.Clear(Color.CornflowerBlue);
			
			Vector2 textPos = new Vector2(
				Resolution.TitleSafeArea.Left,
				Resolution.TitleSafeArea.Top);
			
			spriteBatch.Begin();

			InstructionFont.Write(string.Format("Save device {0} ready.", saveDevice.IsReady ? "is" : "is not"),
			                      textPos,
			                      Justify.Left,
			                      1.0f,
			                      Color.White,
			                      spriteBatch,
			                      0.0f);

			textPos.Y += InstructionFont.Font.LineSpacing;

			InstructionFont.Write(string.Format("Save device {0} busy.", saveDevice.IsBusy ? "is" : "is not"),
			                      textPos,
			                      Justify.Left,
			                      1.0f,
			                      Color.White,
			                      spriteBatch,
			                      0.0f);
			
			textPos.Y += InstructionFont.Font.LineSpacing;
			
			if (saveDevice.IsReady)
			{
				#if WINDOWS_PHONE
				string instructions = "Tap the screen to save a file.";
				#else
				string instructions = "Press the A button or Z key to save a file.";
				#endif

				InstructionFont.Write(instructions,
				                      textPos,
				                      Justify.Left,
				                      1.0f,
				                      Color.White,
				                      spriteBatch,
				                      0.0f);

				textPos.Y += InstructionFont.Font.LineSpacing;

				#if WINDOWS_PHONE
				instructions = "Tap the screen to save a file.";
				#else
				instructions = "Press the B button or X key to load a file.";
				#endif

				InstructionFont.Write(instructions,
				                      textPos,
				                      Justify.Left,
				                      1.0f,
				                      Color.White,
				                      spriteBatch,
				                      0.0f);
			}
			
			spriteBatch.End();
			
			base.Draw(gameTime);
		}
	}
}
