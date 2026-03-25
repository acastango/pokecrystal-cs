namespace PokeCrystal.Game.Scenes;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// A discrete game state that owns its own update/draw cycle.
/// Managed by SceneManager; transitions use palette fades.
/// </summary>
public interface IScene
{
    void OnEnter();
    void OnExit();
    void Update(GameTime gameTime);
    void Draw(SpriteBatch spriteBatch);
}
