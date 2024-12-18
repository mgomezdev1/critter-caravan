using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

#nullable enable
namespace Extensions
{
    public static class AsyncExtensions
    {
        public static async Task ToTask(this AsyncOperation operation)
        {
            await operation;
        }
    }
}