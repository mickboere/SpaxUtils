using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Class containing several tests for the <see cref="IDependencyManager"/> created by the <see cref="GlobalDependencyManager"/>.
	/// </summary>
	public class DependencyManagerTests : MonoBehaviour
	{
		private IDependencyManager dependencyManager;

		protected void Awake()
		{
			// Get dependency locator instance
			dependencyManager = GlobalDependencyManager.Instance;

			SingularDependenciesTests();
			CircularDependencyTest();
		}

		private void SingularDependenciesTests()
		{
			DebugLog("<<< SINGULAR DEPENDENCY TESTS <<<");

			// Create custom instance of 1 and bind it
			DebugLog("Create and bind 1");
			var td1 = new TestDependency_1();
			dependencyManager.Bind(td1);

			DebugLog("Request 1");
			var td1Get = dependencyManager.Get<TestDependency_1>();

			DebugLog("Request for 2 to be created and returned");
			var td2 = dependencyManager.Get<TestDependency_2>();

			DebugLog("Request for 3 to be created and returned");
			var td3 = dependencyManager.Get<TestDependency_3>();

			DebugLog("Request for 4 to be created and returned");
			var td4 = dependencyManager.Get<TestDependency_4>();

			DebugLog(">>> COMPLETED SINGULAR DEPENDENCY TESTS >>>");
		}

		private void CircularDependencyTest()
		{
			DebugLog("<<< CIRCULAR DEPENDENCY TEST <<<");
			// Request 5
			var td5 = dependencyManager.Get<TestDependency_5>();
			DebugLog(">>> COMPLETED CIRCULAR DEPENDENCY TEST >>>");
		}

		public static void DebugLog(string message, LogType logType = LogType.Log)
		{
			SpaxDebug.Log("[TEST] ", message, logType, Color.magenta);
		}

		#region Test Classes
		// Requires nothing (should pass)
		public class TestDependency_1
		{
			public TestDependency_1()
			{
				DebugLog($"Created ({this}) with 0 arguments.");
			}
		}

		// Requires 1 (should pass)
		public class TestDependency_2
		{
			public TestDependency_2(TestDependency_1 testDependency_1)
			{
				DebugLog($"Created ({this}) with 1 argument: {testDependency_1}");
			}
		}

		// Requires 2 (should pass)
		public class TestDependency_3
		{
			public TestDependency_3(TestDependency_2 testDependency_2)
			{
				DebugLog($"Created ({this}) with 1 argument: {testDependency_2}");
			}
		}

		// Requires 1 and 2 (should pass)
		public class TestDependency_4
		{
			public TestDependency_4(TestDependency_1 testDependency_1, TestDependency_2 testDependency_2)
			{
				DebugLog($"Created ({this}) with 2 arguments: {testDependency_1}, {testDependency_2}");
			}
		}

		// Requires 6 (should fail, circular dependency)
		public class TestDependency_5
		{
			public TestDependency_5(TestDependency_6 testDependency_6)
			{
				DebugLog($"Created ({this}) with 1 arguments: {testDependency_6}");
			}
		}

		// Requires 5 (should fail, circular dependency)
		public class TestDependency_6
		{
			public TestDependency_6(TestDependency_5 testDependency_5)
			{
				DebugLog($"Created ({this}) with 1 arguments: {testDependency_5}");
			}
		}
		#endregion
	}
}