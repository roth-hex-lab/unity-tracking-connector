using UnityEditor;
using NUnit.Framework;

namespace HEXLab.Hextrackingconnector.Editor.Tests
{
    class EditorTests
    {
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
    }
}
