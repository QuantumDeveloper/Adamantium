using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Adamantium.Core;
using Adamantium.Engine.Templates.Lights;
using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.Engine.Rendering;
using Adamantium.Graphics;
using Adamantium.Graphics.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Managers;

public class LightManager
{
    private readonly EntityWorld entityWorld;

    private List<Light> lights;
    //private Effect depthWriter;
    //private Universe universe;

    private object _syncObj = new object();

    private ReadOnlyCollection<Light> _lights;
    public ReadOnlyCollection<Light> Lights => _lights;

    public List<Light> SpotLights { get; private set; }
    public List<Light> PointLights { get; private set; }
    public List<Light> DirectionalLights { get; private set; }

    private EntityGroup _lightsGroup;

    private Entity SpotLightMesh;
    private Entity PointLightMesh;

    private MeshData spotLightRenderer;
    private MeshData pointLightRenderer;

    public LightManager(EntityWorld entityWorld)
    {
        this.entityWorld = entityWorld;
        //depthWriter = universe.Content.Load<Effect>("Effects/DeferredShading/DepthWriter");
        lights = new List<Light>();
        _lights = new ReadOnlyCollection<Light>(lights);
        _lightsGroup = new EntityGroup("Lights");
        entityWorld.EntityManager.AddGroup(_lightsGroup);

        SpotLightMesh = new SpotLightMeshTemplate().BuildEntity();
        PointLightMesh = new PointLightMeshTemplate().BuildEntity();

        spotLightRenderer = SpotLightMesh.GetComponent<MeshData>();
        pointLightRenderer = PointLightMesh.GetComponent<MeshData>();

        DirectionalLights = new List<Light>();
        SpotLights = new List<Light>();
        PointLights = new List<Light>();
    }

    // Lights change only on add/remove, so the typed lists are rebuilt only when dirty.
    private bool _lightsDirty = true;

    public void Update()
    {
        if (!_lightsDirty) return;
        _lightsDirty = false;

        DirectionalLights = _lights.Where(x => x.Type == LightType.Directional).ToList();
        SpotLights = _lights.Where(x => x.Type == LightType.Spot).ToList();
        PointLights = _lights.Where(x => x.Type == LightType.Point).ToList();
    }

    public Entity CreateLight(Vector3 position, LightType type, string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            name = type.ToString();
        }

        var light = new LightTemplate().BuildEntity(null, name, type);
        light.Transform.Position = position;
        AddLight(light);
        return light;
    }

    public void AddLight(Entity light)
    {
        var lightComponent = light.GetComponent<Light>();
        if (lightComponent == null)
            return;

        _lightsGroup.Add(light);

        switch (lightComponent.Type)
        {
            case LightType.Directional:
                DirectionalLights.Add(lightComponent);
                break;
            case LightType.Point:
                PointLights.Add(lightComponent);
                break;
            case LightType.Spot:
                SpotLights.Add(lightComponent);
                break;
        }

        lock (_syncObj)
        {
            lights.Add(lightComponent);
        }
        _lightsDirty = true;

        entityWorld.EntityManager.AddEntity(light);
    }

    public void RemoveLight(Entity light)
    {
        _lightsGroup.Remove(light);

        var lightComponent = light.GetComponent<Light>();
        if (lightComponent == null)
            return;

        lights.Remove(lightComponent);
        _lightsDirty = true;
    }

    public bool Contains(Entity light)
    {
        return _lightsGroup.Contains(light);
    }

    public void DrawPointLightMesh(GraphicsDevice device, MeshGeometryCache geometryCache, AppTime appTime)
    {
        geometryCache.DrawMesh(device, pointLightRenderer);
    }

    public void DrawSpotLightMesh(GraphicsDevice device, MeshGeometryCache geometryCache, AppTime appTime)
    {
        geometryCache.DrawMesh(device, spotLightRenderer);
    }
}
