using System;

namespace GravityFire {
	using System.Collections.Generic;
	using System.Linq;
	using SharpVk;

	public class Game {

		public static Game game;

		public Window window;
		public bool run = true;
		public Instance vkInstance;
		public Surface vkSurface;
		public PhysicalDevice vkPhysDevice;
		public Device vkDevice;
		InputHandler input = new InputHandler();

		public Game() {
			window = new Window(1600, 1000);
		}

		public void Run() {
			window.Init();
			while (run) {
				input.Update();
			}
		}

		void InitVulkan() {
			CreateVKInstance();
			CreateSurface();
			PickRenderDevice();
			CreateVKDevice();
		}

		void CreateVKInstance() {
			vkInstance = Instance.Create(new InstanceCreateInfo {
																	ApplicationInfo = new ApplicationInfo {
																											  ApplicationName = "Gravity Fire",
																											  ApplicationVersion = new Version(0, 0, 0),
																											  ApiVersion = new Version(1, 0, 0)
																										  },
																	EnabledExtensionNames = new[] {
																									  KhrSurface.ExtensionName,
																									  KhrWin32Surface.ExtensionName
																								  }
																});
		}

		void CreateSurface() {
			vkSurface = Surface.CreateFromHandle(vkInstance, (ulong) window.GetVulkanSurface((IntPtr) vkInstance.RawHandle.ToUInt64()).ToInt64());
		}

		void PickRenderDevice() {
			foreach (PhysicalDevice dev in vkInstance.EnumeratePhysicalDevices()) {
				PhysicalDeviceProperties prop = dev.GetProperties();
				if (prop.ApiVersion.Major >= 1) {
					vkPhysDevice = dev;
					break;
				}
			}
			if (vkPhysDevice is null) {
				throw new Exception("Couldn't find suitable render device.");
			}
		}

		private struct QueueFamilyIndices {
			public uint? GraphicsFamily;
			public uint? PresentFamily;

			public IEnumerable<uint> Indices {
				get {
					if (this.GraphicsFamily.HasValue) {
						yield return this.GraphicsFamily.Value;
					}

					if (this.PresentFamily.HasValue && this.PresentFamily != this.GraphicsFamily) {
						yield return this.PresentFamily.Value;
					}
				}
			}

			public bool IsComplete {
				get {
					return this.GraphicsFamily.HasValue
						   && this.PresentFamily.HasValue;
				}
			}
		}

		void CreateVKDevice() {

			QueueFamilyIndices queueFams = FindQueueFamilies(vkPhysDevice);
			vkDevice = vkPhysDevice.CreateDevice(new DeviceCreateInfo {
																		  QueueCreateInfos = queueFams.Indices
																									  .Select(i => new DeviceQueueCreateInfo {
																																				   QueueFamilyIndex = i,
																																				   QueuePriorities = new[] { 1f }
																																			   }).ToArray(),
																		  EnabledExtensionNames = new[] {KhrSwapchain.ExtensionName}
																	  });
		}

		QueueFamilyIndices FindQueueFamilies(PhysicalDevice device) {
			QueueFamilyIndices indices = new QueueFamilyIndices();

			QueueFamilyProperties[] queueFamilies = device.GetQueueFamilyProperties();

			for (uint index = 0; index < queueFamilies.Length && !indices.IsComplete; index++) {
				if (queueFamilies[index].QueueFlags.HasFlag(QueueFlags.Graphics)) {
					indices.GraphicsFamily = index;
				}

				if (device.GetSurfaceSupport(index, vkSurface)) {
					indices.PresentFamily = index;
				}
			}

			return indices;
		}

		void Clean() {
			vkInstance.Destroy();
			vkDevice.Destroy();
			window.Destroy();
		}

	}
}