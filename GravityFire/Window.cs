namespace GravityFire {
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.Tracing;
	using System.Linq;
	using System.Runtime.InteropServices;
	using SDL2;
	using Walker.Data.Geometry.Generic.Plane;

	public partial class Window {
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
			SDL.SDL_bool b = SDL.SDL_Vulkan_CreateSurface(ptr, vkInstance, out IntPtr res);
			if (b == SDL.SDL_bool.SDL_FALSE) {
				throw new SDLException("Couldn't create Vulkan surface");
			}
			return res;
		}

		public List<string> GetVulkanExtensionNames() {
			if (SDL.SDL_Vulkan_GetInstanceExtensions(ptr, out uint eCount, null) == SDL.SDL_bool.SDL_FALSE) {
				throw new SDLException("Vulkan_GetInstanceExtensions() [get count]");
			}
			IntPtr[] ptrs = new IntPtr[eCount];
			if (SDL.SDL_Vulkan_GetInstanceExtensions(ptr, out eCount, ptrs) == SDL.SDL_bool.SDL_FALSE) {
				throw new SDLException("Vulkan_GetInstanceExtensions() [get names]");
			}
			return ptrs.Select(p => Marshal.PtrToStringUTF8(p)).ToList();
		}

		public void Destroy() {
			SDL.SDL_DestroyWindow(ptr);
		}

	}
}