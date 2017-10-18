namespace GravityFire {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using SharpVk;
	using Buffer = System.Buffer;
	using Version = SharpVk.Version;

	public class Game {
		public static Game game;
		public const uint Width = 1600, Height = 1000;

		public Window window;
		public bool run = true;
		public Instance vkInstance;
		public Surface vkSurface;
		public PhysicalDevice vkPhysDevice;
		public Device vkDevice;
		public Queue graphicsQueue;
		public Queue presentQueue;
		public Swapchain swapChain;
		public Image[] swapChainImgs;
		public Format swapChainFormat;
		public Extent2D swapChainExtent;
		InputHandler input = new InputHandler();
		ImageView[] swapChainImgViews;
		RenderPass renderPass;
		ShaderModule vertShader;
		ShaderModule fragShader;
		PipelineLayout pipelineLayout;
		Pipeline pipeline;

		public Game() {
			window = new Window(Width, Height);
		}

		public void Run() {
			InitVulkan();
			window.Init();
			while (run) { input.Update(); }
		}

		void InitVulkan() {
			CreateVKInstance();
			CreateSurface();
			PickRenderDevice();
			CreateVKDevice();
			CreateSwapChain();
			CreateImageViews();
			CreateRenderPass();
		}

		void CreateVKInstance() {
			vkInstance = Instance.Create(new InstanceCreateInfo {
				                                                    ApplicationInfo = new ApplicationInfo {
					                                                                                          ApplicationName =
						                                                                                          "Gravity Fire",
					                                                                                          ApplicationVersion =
						                                                                                          new Version(0, 0, 0),
					                                                                                          ApiVersion =
						                                                                                          new Version(1, 0, 0)
				                                                                                          },
				                                                    EnabledExtensionNames = new[] {
					                                                                                  KhrSurface.ExtensionName,
					                                                                                  KhrWin32Surface.ExtensionName
				                                                                                  }
			                                                    });
		}

		void CreateSurface() {
			vkSurface = Surface.CreateFromHandle(vkInstance,
			                                     (ulong) window.GetVulkanSurface((IntPtr) vkInstance.RawHandle.ToUInt64())
			                                                   .ToInt64());
		}

		void PickRenderDevice() {
			foreach (PhysicalDevice dev in vkInstance.EnumeratePhysicalDevices()) {
				PhysicalDeviceProperties prop = dev.GetProperties();
				if (prop.ApiVersion.Major >= 1) {
					vkPhysDevice = dev;
					break;
				}
			}
			if (vkPhysDevice is null) { throw new Exception("Couldn't find suitable render device."); }
		}

		private struct QueueFamilyIndices {
			public uint? GraphicsFamily;
			public uint? PresentFamily;

			public IEnumerable<uint> Indices {
				get {
					if (GraphicsFamily.HasValue) { yield return GraphicsFamily.Value; }

					if (PresentFamily.HasValue && PresentFamily != GraphicsFamily) { yield return PresentFamily.Value; }
				}
			}

			public bool IsComplete => GraphicsFamily.HasValue
			                          && PresentFamily.HasValue;
		}

		struct SwapChainSupportDetails {
			public SurfaceCapabilities Capabilities;
			public SurfaceFormat[] Formats;
			public PresentMode[] PresentModes;
		}

		void CreateVKDevice() {
			QueueFamilyIndices queueFams = FindQueueFamilies(vkPhysDevice);
			vkDevice = vkPhysDevice.CreateDevice(new DeviceCreateInfo {
				                                                          QueueCreateInfos = queueFams.Indices
				                                                                                      .Select(i =>
					                                                                                              new
						                                                                                              DeviceQueueCreateInfo {
							                                                                                                                    QueueFamilyIndex
								                                                                                                                    = i,
							                                                                                                                    QueuePriorities
								                                                                                                                    = new
									                                                                                                                      [] {
										                                                                                                                      1f
									                                                                                                                      }
						                                                                                                                    })
				                                                                                      .ToArray(),
				                                                          EnabledExtensionNames = new[] {KhrSwapchain.ExtensionName}
			                                                          });
			graphicsQueue = vkDevice.GetQueue(queueFams.GraphicsFamily.Value, 0);
			presentQueue = vkDevice.GetQueue(queueFams.PresentFamily.Value, 0);
		}

		QueueFamilyIndices FindQueueFamilies(PhysicalDevice device) {
			QueueFamilyIndices indices = new QueueFamilyIndices();

			QueueFamilyProperties[] queueFamilies = device.GetQueueFamilyProperties();

			for (uint index = 0; index < queueFamilies.Length && !indices.IsComplete; index++) {
				if (queueFamilies[index].QueueFlags.HasFlag(QueueFlags.Graphics)) { indices.GraphicsFamily = index; }

				if (device.GetSurfaceSupport(index, vkSurface)) { indices.PresentFamily = index; }
			}

			return indices;
		}

		void CreateSwapChain() {
			SwapChainSupportDetails details = QuerySwapChainSupport(vkPhysDevice);
			uint imageCount = details.Capabilities.MinImageCount + 1;
			if (details.Capabilities.MaxImageCount > 0 && imageCount > details.Capabilities.MaxImageCount) {
				imageCount = details.Capabilities.MaxImageCount;
			}

			SurfaceFormat surfaceFormat = ChooseSwapSurfaceFormat(details.Formats);

			QueueFamilyIndices queueFamilies = FindQueueFamilies(vkPhysDevice);

			uint[] indices = queueFamilies.Indices.ToArray();

			Extent2D extent = ChooseSwapExtent(details.Capabilities);

			swapChain = vkDevice.CreateSwapchain(new SwapchainCreateInfo {
				                                                             Surface = vkSurface,
				                                                             Flags = SwapchainCreateFlags.None,
				                                                             PresentMode = ChooseSwapPresentMode(details.PresentModes),
				                                                             MinImageCount = imageCount,
				                                                             ImageExtent = extent,
				                                                             ImageUsage = ImageUsageFlags.ColorAttachment,
				                                                             PreTransform = details.Capabilities.CurrentTransform,
				                                                             ImageArrayLayers = 1,
				                                                             ImageSharingMode = indices.Length == 1
					                                                                                ? SharingMode.Exclusive
					                                                                                : SharingMode.Concurrent,
				                                                             QueueFamilyIndices = indices,
				                                                             ImageFormat = surfaceFormat.Format,
				                                                             ImageColorSpace = surfaceFormat.ColorSpace,
				                                                             Clipped = true,
				                                                             CompositeAlpha = CompositeAlphaFlags.Opaque,
				                                                             OldSwapchain = swapChain
			                                                             });

			swapChainImgs = swapChain.GetImages();
			swapChainFormat = surfaceFormat.Format;
			swapChainExtent = extent;
		}

		void CreateImageViews() {
			swapChainImgViews = swapChainImgs.Select(img => vkDevice.CreateImageView(new ImageViewCreateInfo {
				                                                                                                 Components = ComponentMapping.Identity,
				                                                                                                 Format = swapChainFormat,
				                                                                                                 Image = img,
				                                                                                                 Flags = ImageViewCreateFlags.None,
				                                                                                                 ViewType = ImageViewType.ImageView2d,
				                                                                                                 SubresourceRange = new ImageSubresourceRange {
					                                                                                                                                              AspectMask = ImageAspectFlags.Color,
					                                                                                                                                              BaseMipLevel = 0,
					                                                                                                                                              LevelCount = 1,
					                                                                                                                                              BaseArrayLayer = 0,
					                                                                                                                                              LayerCount = 1
				                                                                                                                                              }
			                                                                                                 })).ToArray();
		}

		SwapChainSupportDetails QuerySwapChainSupport(PhysicalDevice device) => new SwapChainSupportDetails {
			                                                                                                    Capabilities =
				                                                                                                    device
					                                                                                                    .GetSurfaceCapabilities(vkSurface),
			                                                                                                    Formats = device
				                                                                                                    .GetSurfaceFormats(vkSurface),
			                                                                                                    PresentModes =
				                                                                                                    device
					                                                                                                    .GetSurfacePresentModes(vkSurface)
		                                                                                                    };

		static SurfaceFormat ChooseSwapSurfaceFormat(IList<SurfaceFormat> availableFormats) {
			if (availableFormats.Count == 1 && availableFormats[0].Format == Format.Undefined) {
				return new SurfaceFormat {
					                         Format = Format.B8G8R8A8UNorm,
					                         ColorSpace = ColorSpace.SrgbNonlinear
				                         };
			}

			foreach (SurfaceFormat format in availableFormats) {
				if (format.Format == Format.B8G8R8A8UNorm && format.ColorSpace == ColorSpace.SrgbNonlinear) { return format; }
			}

			return availableFormats[0];
		}

		public Extent2D ChooseSwapExtent(SurfaceCapabilities capabilities) =>
			capabilities.CurrentExtent.Width != uint.MaxValue
				? capabilities.CurrentExtent
				: new Extent2D {
					               Width = Math.Max(capabilities.MinImageExtent.Width,
					                                Math.Min(capabilities.MaxImageExtent.Width, Width)),
					               Height = Math.Max(capabilities.MinImageExtent.Height,
					                                 Math.Min(capabilities.MaxImageExtent.Height, Height))
				               };

		PresentMode ChooseSwapPresentMode(PresentMode[] availablePresentModes) =>
			availablePresentModes.Contains(PresentMode.Mailbox)
				? PresentMode.Mailbox
				: PresentMode.Fifo;

		void CreateRenderPass() {
			renderPass = vkDevice.CreateRenderPass(new RenderPassCreateInfo {
				                                                                Attachments = new[] {
					                                                                                    new AttachmentDescription {
						                                                                                                              Format = swapChainFormat,
						                                                                                                              Samples = SampleCountFlags.SampleCount1,
						                                                                                                              LoadOp = AttachmentLoadOp.Clear,
						                                                                                                              StoreOp = AttachmentStoreOp.Store,
						                                                                                                              StencilLoadOp = AttachmentLoadOp.DontCare,
						                                                                                                              StencilStoreOp = AttachmentStoreOp.DontCare,
						                                                                                                              InitialLayout = ImageLayout.Undefined,
						                                                                                                              FinalLayout = ImageLayout.PresentSource
					                                                                                                              }
				                                                                                    },
				                                                                Subpasses = new[] {
					                                                                                  new SubpassDescription {
						                                                                                                         DepthStencilAttachment = new AttachmentReference {
							                                                                                                                                                          Attachment = Constants.AttachmentUnused
						                                                                                                                                                          },
						                                                                                                         PipelineBindPoint = PipelineBindPoint.Graphics,
						                                                                                                         ColorAttachments = new[] {
							                                                                                                                                  new AttachmentReference {
								                                                                                                                                                          Attachment = 0,
								                                                                                                                                                          Layout = ImageLayout.ColorAttachmentOptimal
							                                                                                                                                                          }
						                                                                                                                                  }
					                                                                                                         }
				                                                                                  },
				                                                                Dependencies = new[] {
					                                                                                     new SubpassDependency {
						                                                                                                           SourceSubpass = Constants.SubpassExternal,
						                                                                                                           DestinationSubpass = 0,
						                                                                                                           SourceStageMask = PipelineStageFlags.BottomOfPipe,
						                                                                                                           SourceAccessMask = AccessFlags.MemoryRead,
						                                                                                                           DestinationStageMask = PipelineStageFlags.ColorAttachmentOutput,
						                                                                                                           DestinationAccessMask = AccessFlags.ColorAttachmentRead | AccessFlags.ColorAttachmentWrite
					                                                                                                           },
					                                                                                     new SubpassDependency {
						                                                                                                           SourceSubpass = 0,
						                                                                                                           DestinationSubpass = Constants.SubpassExternal,
						                                                                                                           SourceStageMask = PipelineStageFlags.ColorAttachmentOutput,
						                                                                                                           SourceAccessMask = AccessFlags.ColorAttachmentRead | AccessFlags.ColorAttachmentWrite,
						                                                                                                           DestinationStageMask = PipelineStageFlags.BottomOfPipe,
						                                                                                                           DestinationAccessMask = AccessFlags.MemoryRead
					                                                                                                           }
				                                                                                     }
			                                                                });
		}

		void CreateShaderModules() {
			int codeSize;
			uint[] vertShaderData = LoadShaderData(@".\Shaders\vert.spv", out codeSize);
			vertShader = vkDevice.CreateShaderModule(new ShaderModuleCreateInfo {
				                                                                       Code = vertShaderData,
				                                                                       CodeSize = codeSize
			                                                                       });

			uint[] fragShaderData = LoadShaderData(@".\Shaders\frag.spv", out codeSize);
			fragShader = vkDevice.CreateShaderModule(new ShaderModuleCreateInfo {
				                                                                       Code = fragShaderData,
				                                                                       CodeSize = codeSize
			                                                                       });
		}

		static uint[] LoadShaderData(string filePath, out int codeSize) {
			byte[] fileBytes = File.ReadAllBytes(filePath);
			uint[] shaderData = new uint[(int)Math.Ceiling(fileBytes.Length / 4f)];

			Buffer.BlockCopy(fileBytes, 0, shaderData, 0, fileBytes.Length);

			codeSize = fileBytes.Length;

			return shaderData;
		}

		void CreateGraphicsPipeline() {
            pipelineLayout = vkDevice.CreatePipelineLayout(new PipelineLayoutCreateInfo());

	        pipeline = vkDevice.CreateGraphicsPipelines(null, new[] {
		                                                                new GraphicsPipelineCreateInfo {
			                                                                                               Layout = pipelineLayout,
			                                                                                               RenderPass = renderPass,
			                                                                                               Subpass = 0,
			                                                                                               VertexInputState = new PipelineVertexInputStateCreateInfo(),
			                                                                                               InputAssemblyState = new PipelineInputAssemblyStateCreateInfo {
				                                                                                                                                                             PrimitiveRestartEnable = false,
				                                                                                                                                                             Topology = PrimitiveTopology.TriangleList
			                                                                                                                                                             },
			                                                                                               ViewportState = new PipelineViewportStateCreateInfo {
				                                                                                                                                                   Viewports = new[] {
					                                                                                                                                                                     new Viewport {
						                                                                                                                                                                                  X = 0f,
						                                                                                                                                                                                  Y = 0f,
						                                                                                                                                                                                  Width = swapChainExtent.Width,
						                                                                                                                                                                                  Height = swapChainExtent.Height,
						                                                                                                                                                                                  MaxDepth = 1,
						                                                                                                                                                                                  MinDepth = 0
					                                                                                                                                                                                  }
				                                                                                                                                                                     },
				                                                                                                                                                   Scissors = new[] {
					                                                                                                                                                                    new Rect2D {
						                                                                                                                                                                               Offset = new Offset2D(),
						                                                                                                                                                                               Extent = swapChainExtent
					                                                                                                                                                                               }
				                                                                                                                                                                    }
			                                                                                                                                                   },
			                                                                                               RasterizationState = new PipelineRasterizationStateCreateInfo {
				                                                                                                                                                             DepthClampEnable = false,
				                                                                                                                                                             RasterizerDiscardEnable = false,
				                                                                                                                                                             PolygonMode = PolygonMode.Fill,
				                                                                                                                                                             LineWidth = 1,
				                                                                                                                                                             CullMode = CullModeFlags.Back,
				                                                                                                                                                             FrontFace = FrontFace.Clockwise,
				                                                                                                                                                             DepthBiasEnable = false
			                                                                                                                                                             },
			                                                                                               MultisampleState = new PipelineMultisampleStateCreateInfo {
				                                                                                                                                                         SampleShadingEnable = false,
				                                                                                                                                                         RasterizationSamples = SampleCountFlags.SampleCount1,
				                                                                                                                                                         MinSampleShading = 1
			                                                                                                                                                         },
			                                                                                               ColorBlendState = new PipelineColorBlendStateCreateInfo {
				                                                                                                                                                       Attachments = new[] {
					                                                                                                                                                                           new PipelineColorBlendAttachmentState {
						                                                                                                                                                                                                                 ColorWriteMask = ColorComponentFlags.R
						                                                                                                                                                                                                                                  | ColorComponentFlags.G
						                                                                                                                                                                                                                                  | ColorComponentFlags.B
						                                                                                                                                                                                                                                  | ColorComponentFlags.A,
						                                                                                                                                                                                                                 BlendEnable = false,
						                                                                                                                                                                                                                 SourceColorBlendFactor = BlendFactor.One,
						                                                                                                                                                                                                                 DestinationColorBlendFactor = BlendFactor.Zero,
						                                                                                                                                                                                                                 ColorBlendOp = BlendOp.Add,
						                                                                                                                                                                                                                 SourceAlphaBlendFactor = BlendFactor.One,
						                                                                                                                                                                                                                 DestinationAlphaBlendFactor = BlendFactor.Zero,
						                                                                                                                                                                                                                 AlphaBlendOp = BlendOp.Add
					                                                                                                                                                                                                                 }
				                                                                                                                                                                           },
				                                                                                                                                                       LogicOpEnable = false,
				                                                                                                                                                       LogicOp = LogicOp.Copy,
				                                                                                                                                                       BlendConstants = new float[] {0, 0, 0, 0}
			                                                                                                                                                       },
			                                                                                               Stages = new[] {
				                                                                                                              new PipelineShaderStageCreateInfo {
					                                                                                                                                                Stage = ShaderStageFlags.Vertex,
					                                                                                                                                                Module = vertShader,
					                                                                                                                                                Name = "main"
				                                                                                                                                                },
				                                                                                                              new PipelineShaderStageCreateInfo {
					                                                                                                                                                Stage = ShaderStageFlags.Fragment,
					                                                                                                                                                Module = fragShader,
					                                                                                                                                                Name = "main"
				                                                                                                                                                }
			                                                                                                              }
		                                                                                               }
	                                                                }).Single();
        }

		void Clean() {
			foreach (ImageView view in swapChainImgViews) { view.Destroy(); }
			vkSurface.Destroy();
			vkInstance.Destroy();
			vkDevice.Destroy();
			window.Destroy();
		}

		public static void Main(string[] args) {
			game = new Game();
			game.Run();
		}
	}
}