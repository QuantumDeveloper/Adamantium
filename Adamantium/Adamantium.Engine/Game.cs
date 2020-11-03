using System;
using System.Collections.Generic;
using Adamantium.Engine.Services;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Processors;
using Adamantium.Game;
using Adamantium.Game.GameInput;

namespace Adamantium.Engine
{
    public class Game : GameBase
    {
        private Dictionary<GameWindow, EntityProcessor> drawSystems;

        public EntityWorld EntityWorld { get; }

        public InputService InputService { get; private set; }

        public ToolsService ToolsService { get; private set; }
        public LightService LightService { get; private set; }
        public CameraService CameraService { get; private set; }
        public GamePlayManager GamePlayManager { get; private set; }
        public GraphicsDeviceManager GraphicsDeviceManager { get; set; }

        public Game()
        {
            GraphicsDeviceManager = new GraphicsDeviceManager(this);
            EntityWorld = new EntityWorld(Services);
            Services.Add(EntityWorld);
            Content.Readers.Add(typeof(Entity), new ModelContentReader());
            InputService = new InputService(this, Services);
            GamePlayManager = new GamePlayManager(Services);
            WindowRemoved += Game_WindowRemoved;
            Stopped += Game_Stopped;
            drawSystems = new Dictionary<GameWindow, EntityProcessor>();
        }

        private void Game_Stopped(object sender, EventArgs e)
        {
            EntityWorld.Reset();
        }

        private void Game_WindowRemoved(object sender, GameWindowEventArgs e)
        {
            RemoveRenderProcessor(e.Window);
        }

        private void RemoveRenderProcessor(GameWindow window)
        {
            lock (drawSystems)
            {
                if (drawSystems.ContainsKey(window))
                {
                    EntityWorld.RemoveProcessor(drawSystems[window]);
                    drawSystems.Remove(window);
                }
            }
        }

        public T CreateRenderProcessor<T>(GameWindow window) where T : RenderProcessor
        {
            var system = EntityWorld.CreateProcessor<T>(new object[] { EntityWorld, window });
            lock (drawSystems)
            {
                drawSystems.Add(window, system);
            }

            return system;
        }

        protected override void Initialize()
        {
            base.Initialize();
            ToolsService = new ToolsService(this);
            LightService = new LightService(this);
            CameraService = new CameraService(this);
        }

    }
}
