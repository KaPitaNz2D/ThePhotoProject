/*
Yarn Spinner is licensed to you under the terms found in the file LICENSE.md.
*/

#nullable enable

namespace Yarn.Unity.Attributes
{
    public class YarnEnumParameterAttribute: YarnParameterAttribute
    {
        public string Name { get; set; }

        public YarnEnumParameterAttribute(string name) => Name = name;
    }
}
