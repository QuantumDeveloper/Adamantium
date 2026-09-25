using System;
using System.Threading.Tasks;
using Adamantium.Engine;
using Adamantium.Engine.EntityServices;
using Adamantium.Engine.Templates;
using Adamantium.Engine.Templates.Lights;
using Adamantium.Engine.Tools;
using Adamantium.ECS.Components;
using Adamantium.ECS;
using Adamantium.Game;
using Adamantium.Game.Core;
using Adamantium.Game.Core.Events;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Content;
using Adamantium.Graphics.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.UI.Sandbox
{
    public class AdamantiumGame : Universe
    {
        private readonly SelectTool _selectTool = new();
        private readonly MoveTool _moveTool = new();
        private readonly RotationTool _rotationTool = new();
        private readonly ScaleTool _scaleTool = new();
        private readonly PivotTool _pivotTool = new();
        private Task _startupLoad;
        private InputService _inputService;
        private ToolsService _tools;

        public AdamantiumGame(
            bool enableDynamicRendering,
            bool enableDebug) :
            base(UniverseMode.Primary, enableDebug)
        {
            EventAggregator.GetEvent<UniverseOutputCreatedEvent>().Subscribe(OnWindowCreated);
        }

        public AdamantiumGame(
            IGraphicsDeviceService graphicsDeviceService,
            bool enableDebug) :
            base(UniverseMode.Slave, enableDebug, graphicsDeviceService)
        {
            EventAggregator.GetEvent<UniverseOutputCreatedEvent>().Subscribe(OnWindowCreated);
        }

        private void OnWindowCreated(UniverseOutput output)
        {
            var renderingService = CreateRenderService<RenderingService>(output);
            renderingService.AttachProcessor(new ForwardRenderingProcessor());
            renderingService.AttachProcessor(new EditorOverlayProcessor(_tools));
        }

        protected override void Initialize()
        {
            base.Initialize();
            Satellites.Add(new Selection());
            Satellites.Add(new Observatory(this, EntityWorld));
            InitializeGameResources();
        }

        /// <summary>The tool the mouse works with from the next frame on.</summary>
        public void UseTool(EditTool tool)
        {
            _tools.Tool = tool switch
            {
                EditTool.Move => _moveTool,
                EditTool.Rotate => _rotationTool,
                EditTool.Scale => _scaleTool,
                EditTool.Pivot => _pivotTool,
                _ => _selectTool
            };
        }

        protected override void LoadContent()
        {
            base.LoadContent();
            // Kept, not dropped: the task is what anyone waiting for the startup model has to hold on to, and it is
            // also what keeps this from starting a second load - LoadContent runs again when the device is recreated.
            _startupLoad ??= LoadModels();
        }

        private void InitializeGameResources()
        {
            try
            {
                _inputService = EntityWorld.CreateService<InputService>(EntityWorld);
                EntityWorld.CreateService<TransformService>(EntityWorld);
                _tools = EntityWorld.CreateService<ToolsService>(EntityWorld);
                _tools.AttachProcessor(new SelectionOutline());
                _tools.AttachProcessor(new EntityIcons());
                _tools.AttachProcessor(new OrientationCube());
                _tools.Tool = _selectTool;
                AddLightsAndCamera();
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }

        private void AddLightsAndCamera()
        {
            var point = new LightTemplate().BuildEntity(null, "Point light", LightType.Point);
            point.Transform.Position = new Vector3(-5, -4, 2);
            point.GetComponent<Light>().Range = 6;
            EntityWorld.EntityManager.AddEntity(point);

            var spot = new LightTemplate().BuildEntity(null, "Spot light", LightType.Spot);
            spot.Transform.Position = new Vector3(5, -6, 6);
            spot.Transform.Rotation = QuaternionF.RotationAxis(Vector3F.UnitX, MathHelper.DegreesToRadians(180));
            var spotLight = spot.GetComponent<Light>();
            spotLight.Range = 8;
            spotLight.OuterSpotAngle = MathHelper.DegreesToRadians(30);
            EntityWorld.EntityManager.AddEntity(spot);

            var camera = new CameraTemplate().BuildEntity(null, "Scene camera", new Vector3(8, -3, -4), Vector3.ForwardLH, -Vector3.Up, 800, 600, 0.1f, 1000f);
            EntityWorld.EntityManager.AddEntity(camera);
        }

        public Task<Entity> ImportModel(SceneData scene)
        {
            return Task.Run(() =>
                EntityWorld.CreateEntityFromTemplate(new EntityImportTemplate(scene, Content, new Vector3(0, 0, 6))));
        }

        public async Task<Entity> ImportModel(String pathToFile, ContentLoadOptions options = null)
        {
            // Reader produces SceneData (parse now, deserialize a baked binary later); entity assembly stays here.
            var scene = await Content.LoadAsync<SceneData>(pathToFile, options);
            return await ImportModel(scene);
        }

        /// <summary>Returns a Task rather than being async void. A void one cannot be awaited and cannot hand its
        /// exception anywhere - a broken model file would have taken the process down without a word.</summary>
        private async Task LoadModels()
        {
            try
            {
                await LoadAndAddModel(SandboxAssets.Models.F15C.F_15C_Eagle_dae);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Failed to load the startup model: {exception}");
            }
        }

        /// <summary>Loads a model file into the scene and makes it the subject the camera follows.</summary>
        public async Task<Entity> LoadAndAddModel(string pathToFile)
        {
            var entity = await ImportModel(pathToFile);
            EntityWorld.EntityManager.AddEntity(entity);

            _inputService.UserControlledEntity = entity;
            return entity;
        }
    }
}