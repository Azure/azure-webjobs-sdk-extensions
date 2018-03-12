// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    using System.Runtime.Serialization;
    using System.Security.Claims;

    /// <summary>
    /// Light-weight representation of a <see cref="Claim"/> object.
    /// </summary>
    [DataContract]
    internal struct ClaimSlim
    {
        [DataMember(Name = "val")]
        public string Value;

        [DataMember(Name = "typ")]
        public string Type;

        public ClaimSlim(Claim claim)
            : this(claim.Type, claim.Value)
        {
        }

        public ClaimSlim(string type, string value)
        {
            this.Type = type;
            this.Value = value;
        }

        public bool IsEmpty
        {
            get { return this.Type == null && this.Value == null; }
        }

        public Claim ToClaim()
        {
            return new Claim(this.Type, this.Value);
        }

        /// <summary>
        /// Gets a string showing the type and value of the claim. This is primarily intended for debugging.
        /// </summary>
        public override string ToString()
        {
            return string.Concat(this.Type, ": ", this.Value);
        }
    }
}
