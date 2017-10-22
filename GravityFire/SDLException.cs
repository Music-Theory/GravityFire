namespace GravityFire {
	using System;
	using SDL2;
	public class SDLException : Exception {

		public SDLException(string msg) : base("SDL: " + msg + " :: " + SDL.SDL_GetError()) { }

	}
}