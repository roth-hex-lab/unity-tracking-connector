using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;

namespace HEXLab.Hextrackingconnector.Editor.Tests 
{
	
	class EditorExampleTest 
	{

		[Test]
		public void EditorSampleTestSimplePasses() 
		{
			// Use the Assert class to test conditions.
		}

		[Test]
		public void SkeletonProviderDrawerExistsForProviderAttribute()
		{
			var drawerType = typeof(CommServerEditor).Assembly.GetType(
				"HEXLab.Hextrackingconnector.Editor.SkeletonProviderDrawer");

			Assert.IsNotNull(drawerType);
			Assert.IsTrue(typeof(PropertyDrawer).IsAssignableFrom(drawerType));
			Assert.IsTrue(drawerType
				.GetCustomAttributes(typeof(CustomPropertyDrawer), inherit: false)
				.Length > 0);
		}

		// A UnityTest behaves like a coroutine in PlayMode
		// and allows you to yield null to skip a frame in EditMode
		[UnityTest]
		public IEnumerator EditorSampleTestWithEnumeratorPasses() 
		{
			// Use the Assert class to test conditions.
			// yield to skip a frame
			yield return null;
		}
	}
}
