
using NeoModLoader.constants;
using UnityEngine;
using NeoModLoader.AndroidCompatibilityModule;
namespace NeoModLoader.api;

/// <summary>
///     This class is made for ncms mod to get <see cref="ModDeclare" /> for themselves
/// </summary>
public class AttachedModComponent : WrappedBehaviour, IMod
{
    private ModDeclare _declare;

    public ModDeclare GetDeclaration()
    {
        return _declare;
    }

    public GameObject GetGameObject()
    {
        return gameObject;
    }

    public string GetUrl()
    {
        return _declare.RepoUrl;   // no fallback: a mod without its own link gets no website button
    }

    public void OnLoad(ModDeclare pModDecl, GameObject pGameObject)
    {
        _declare = pModDecl;
    }
}