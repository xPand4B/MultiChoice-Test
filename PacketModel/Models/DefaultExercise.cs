﻿using System;
using System.Collections.Generic;

namespace PacketModel.Models
{
    [Serializable]
    public class DefaultExercise
    {
        public int ID { get; set; }
        public string Question { get; set; }
        public int ResultIndex { get; set; }
        public List<string> Answers { get; set; } = new List<string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultExercise"/> class.
        /// </summary>
        /// <param name="op"></param>
        public DefaultExercise()
        {
        }
    }
}