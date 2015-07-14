﻿using System;
using System.Collections.Generic;

namespace DAL
{
    public class Show : DbEntry
    {
        public String Title { get; set; }
        public virtual List<Series> SeriesList { get; set; }

        public Show()
        {
             SeriesList = new List<Series>();
        }
    }
}
