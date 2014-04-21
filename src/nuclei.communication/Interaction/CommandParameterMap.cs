﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics;

namespace Nuclei.Communication.Interaction
{
    /// <summary>
    /// Stores information about a single parameter on a command method.
    /// </summary>
    internal sealed class CommandParameterMap
    {
        /// <summary>
        /// The type of the parameter.
        /// </summary>
        private readonly Type m_Type;

        /// <summary>
        /// The name of the parameter.
        /// </summary>
        private readonly string m_Name;

        /// <summary>
        /// The origin of the parameter.
        /// </summary>
        private readonly CommandParameterOrigin m_Orgin;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandParameterMap"/> class.
        /// </summary>
        /// <param name="type">The type of the parameter.</param>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="orgin">The origin of the parameter.</param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="type"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="name"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     Thrown if <paramref name="name"/> is an empty string.
        /// </exception>
        public CommandParameterMap(Type type, string name, CommandParameterOrigin orgin)
        {
            {
                Lokad.Enforce.Argument(() => type);
                Lokad.Enforce.Argument(() => name);
                Lokad.Enforce.Argument(() => name, Lokad.Rules.StringIs.NotEmpty);
            }

            m_Type = type;
            m_Name = name;
            m_Orgin = orgin;
        }

        /// <summary>
        /// Gets the type of the parameter.
        /// </summary>
        public Type Type
        {
            [DebuggerStepThrough]
            get
            {
                return m_Type;
            }
        }

        /// <summary>
        /// Gets the name of the parameter.
        /// </summary>
        public string Name
        {
            [DebuggerStepThrough]
            get
            {
                return m_Name;
            }
        }

        /// <summary>
        /// Gets the orgin of the parameter.
        /// </summary>
        public CommandParameterOrigin Orgin
        {
            [DebuggerStepThrough]
            get
            {
                return m_Orgin;
            }
        }
    }
}