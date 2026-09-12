/*
Yarn Spinner is licensed to you under the terms found in the file LICENSE.md.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#nullable enable

namespace Yarn.Unity.Attributes
{
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    public abstract class YarnParameterAttribute : Attribute
    {
    }
}
