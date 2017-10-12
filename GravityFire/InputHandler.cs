namespace GravityFire {
	using SDL2;

	public class InputHandler : GameObject {

		public void Update() {
			while (SDL.SDL_PollEvent(out SDL.SDL_Event state) != 0) {
				switch (state.type) {
					case SDL.SDL_EventType.SDL_QUIT:
						Game.run = false;
						break;
				}
			}
		}

	}
}