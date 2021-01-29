using System;
using System.Collections.Generic;
using Adamantium.Core.Events;
using Adamantium.Engine.Processors;
using Adamantium.Engine.Services;
using Adamantium.EntityFramework;
using Adamantium.Game;
using Adamantium.Game.Events;
using Adamantium.Game.GameInput;

namespace Adamantium.Engine
{
    public class Game : GameBase
    {
        private Dictionary<GameOutput, EntityProcessor> drawSystems;

        public EntityWorld EntityWorld { get; }

        public InputService InputService { get; private set; }

        public ToolsService ToolsService { get; private set; }
        public LightService LightService { get; private set; }
        public CameraService CameraService { get; private set; }
        public GamePlayManager GamePlayManager { get; private set; }
        public GraphicsDeviceService GraphicsDeviceService { get; set; }
        
        protected IEventAggregator EventAggregator { get; }

        public Game(GameMode mode) : base(mode)
        {
            EventAggregator = Services.Resolve<IEventAggregator>();
            GraphicsDeviceService = new GraphicsDeviceService(this);
            EntityWorld = new EntityWorld(Services);
            Services.RegisterInstance<EntityWorld>(EntityWorld);
            Content.Readers.Add(typeof(Entity), new ModelContentReader());
            InputService = new InputService(this, Services);
            GamePlayManager = new GamePlayManager(Services);
            EventAggregator.GetEvent<GameOutputRemovedEvent>().Subscribe(OnGameOutputRemoved);
            Stopped += Game_Stopped;
            drawSystems = new Dictionary<GameOutput, EntityProcessor>();
        }

        private void Game_Stopped(object sender, EventArgs e)
        {
            EntityWorld.Reset();
        }

        private void OnGameOutputRemoved(GameOutput output)
        {
            RemoveRenderProcessor(output);
        }

        private void RemoveRenderProcessor(GameOutput window)
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

        public T CreateRenderProcessor<T>(GameOutput window) where T : RenderProcessor
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
