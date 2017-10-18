namespace GravityFire {
	using System;
	using SDL2;
	using Walker.Data.Geometry.Generic.Plane;

	public class Window {

		Vector2<uint> size;
		public IntPtr ptr;

		public Window(uint width, uint height) {
			size = new Vector2<uint>(width, height);
		}

		public Window(Vector2<uint> size) {
			this.size = size;
		}

		public void Init() {
			ptr = SDL.SDL_CreateWindow("Gravity Fire", 50, 50, (int) size.X, (int) size.Y, SDL.SDL_WindowFlags.SDL_WINDOW_VULKAN);
		}

		public IntPtr GetVulkanSurface(IntPtr vkInstance) {
			SDL.SDL_Vulkan_CreateSurface(ptr, vkInstance, out IntPtr res);
			return res;
		}

		public void Destroy() {
			SDL.SDL_DestroyWindow(ptr);
		}

	}
}