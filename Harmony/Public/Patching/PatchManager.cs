using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonoMod.Utils;

namespace HarmonyLib.Public.Patching
{
	/// <summary>
	/// A global manager for handling Harmony patch state. Contains information about all patched methods and all
	/// actual <see cref="MethodPatcher"/> instances that handle patching implementation.
	/// </summary>
	///
	public static class PatchManager
	{
		/// <summary>
		/// Patcher resolve event arguments.
		/// </summary>
		///
		public class PatcherResolverEeventArgs : EventArgs
		{
			/// <summary>
			/// Original method that is to be patched.
			/// </summary>
			///
			public MethodBase Original { get; internal set; }

			/// <summary>
			/// Method patcher to use to patch <see cref="Original"/>.
			/// Set this value to specify which one to use.
			/// </summary>
			///
			public MethodPatcher MethodPatcher { get; set; }
		}

		/// <summary>
		/// Method patcher resolve event.
		/// </summary>
		/// <remarks>
		/// When a method is to be patched, this resolver event is called once on the method to determine which
		/// <see cref="MethodPatcher"/> backend to use in order to patch the method.
		/// To make Harmony use the specified backend, set <see cref="PatcherResolverEeventArgs.MethodPatcher"/> to an
		/// instance of the method patcher backend to use.
		/// </remarks>
		///
		public static event EventHandler<PatcherResolverEeventArgs> ResolvePatcher;

		private static readonly Dictionary<MethodBase, PatchInfo> PatchInfos = new Dictionary<MethodBase, PatchInfo>();
		private static readonly Dictionary<MethodBase, MethodPatcher> MethodPatchers = new Dictionary<MethodBase, MethodPatcher>();

		static PatchManager()
		{
			ResolvePatcher += ManagedMethodPatcher.TryResolve;
			ResolvePatcher += NativeDetourMethodPatcher.TryResolve;
		}

		/// <summary>
		/// Creates or gets an existing instance of <see cref="MethodPatcher"/> that handles patching the method.
		/// </summary>
		/// <param name="methodBase">Method to patch.</param>
		/// <returns>Instance of <see cref="MethodPatcher"/> that handles patching the method.</returns>
		/// <exception cref="NullReferenceException">No suitable patcher found for the method.</exception>
		///
		public static MethodPatcher GetMethodPatcher(this MethodBase methodBase)
		{
			lock (MethodPatchers)
			{
				if (MethodPatchers.TryGetValue(methodBase, out var methodPatcher))
					return methodPatcher;
				var args = new PatcherResolverEeventArgs {Original = methodBase};
				ResolvePatcher?.Invoke(null, args);
				if (args.MethodPatcher == null)
					throw new NullReferenceException($"No suitable patcher found for {methodBase.FullDescription()}");
				return MethodPatchers[methodBase] = args.MethodPatcher;
			}
		}

		public static PatchInfo GetPatchInfo(this MethodBase methodBase)
		{
			lock (PatchInfos)
				return PatchInfos.GetValueSafe(methodBase);
		}

		public static PatchInfo ToPatchInfo(this MethodBase methodBase)
		{
			lock (PatchInfos)
			{
				if (PatchInfos.TryGetValue(methodBase, out var info))
					return info;

				return PatchInfos[methodBase] = new PatchInfo();
			}
		}

		public static IEnumerable<MethodBase> GetPatchedMethods()
		{
			lock (PatchInfos)
				return PatchInfos.Keys.ToList();
		}

		public static void ClearAllPatcherResolvers()
		{
			ResolvePatcher = null;
		}
	}
}
