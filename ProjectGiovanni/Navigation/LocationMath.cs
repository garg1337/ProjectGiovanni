using ProjectGiovanni.Data;
using System;
using System.Collections.Generic;

namespace ProjectGiovanni.Navigation
{
    public static class LocationMath
    {
        // degree bearings
        public const double NORTH = 0.0;
        public const double EAST = 90.0;
        public const double SOUTH = 180.0;
        public const double WEST = 270.0;

        // radius of player heartbeat in km. right now its 70 meters
        public const double PULSE_RADIUS = 0.07;

        // radius of the earth in km
        public const double RADIUS_OF_EARTH = 6378.1;

        // horizontal distance between the hexagons we're going to scan. 
        public static double XDISTANCE
        {
            get
            {
                // formula for width of a hexagon with all sides of length PULSE_RADIUS
                return PULSE_RADIUS * Math.Sqrt(3.0);
            }
        }

        // vertical distance between the hexagons we're going to scan. 
        public static double YDISTANCE
        {
            get
            {
                // formula height of a hexagon with all sides of length PULSE_RADIUS
                return 3.0 * (PULSE_RADIUS/2.0);
            }
        }

        // Given an initial location, a distance in kilometers, and a bearing in degrees,
        // calculate the new location. 
        public static Location CalculateNewLocationFromDistanceAndBearing(Location origin, double distance, double bearing)
        {
            double bearingRadians = DegreesToRadians(bearing);
            double latitudeRadians = DegreesToRadians(origin.Latitude);
            double longitudeRadians = DegreesToRadians(origin.Longitude);
            // See http://stackoverflow.com/questions/7222382/get-lat-long-given-current-point-distance-and-bearing for formula

            double newLatititudeRadians = Math.Asin(Math.Sin(latitudeRadians) * Math.Cos(distance / RADIUS_OF_EARTH) +
                                   Math.Cos(latitudeRadians) * Math.Sin(distance / RADIUS_OF_EARTH) * Math.Cos(bearingRadians));
            double newLongitudeRadians = longitudeRadians + Math.Atan2(Math.Sin(bearingRadians) * Math.Sin(distance / RADIUS_OF_EARTH) * Math.Cos(latitudeRadians),
                                  Math.Cos(distance / RADIUS_OF_EARTH) - Math.Sin(latitudeRadians) * Math.Sin(newLatititudeRadians));
            // now convert back to degrees
            double newLatitude = RadiansToDegrees(newLatititudeRadians);

            double newLongitude = RadiansToDegrees(newLongitudeRadians);
            return new Location()
            {
                Latitude = newLatitude,
                Longitude = newLongitude
            };
        }

        public static IEnumerable<Location> GenerateLocationSteps(Location initialLocation, int stepCount)
        {
            // first step is the initial location
            yield return initialLocation;

            int ring = 1;
            Location nextLocation = initialLocation;

            while (ring < stepCount)
            {
                // start at the top left
                nextLocation = CalculateNewLocationFromDistanceAndBearing(nextLocation, YDISTANCE, NORTH);
                nextLocation = CalculateNewLocationFromDistanceAndBearing(nextLocation, XDISTANCE / 2.0, WEST);

                for (int direction = 0; direction < 6; direction++)
                {
                    for (int i = 0; i < ring; i++)
                    {
                        switch (direction)
                        {
                            // move right
                            case 0:
                                nextLocation = CalculateNewLocationFromDistanceAndBearing(nextLocation, XDISTANCE, EAST);
                                break;
                            // move down + right
                            case 1:
                                nextLocation = CalculateNewLocationFromDistanceAndBearing(nextLocation, YDISTANCE, SOUTH);
                                nextLocation = CalculateNewLocationFromDistanceAndBearing(nextLocation, XDISTANCE / 2.0, EAST);
                                break;
                            // move down + left
                            case 2:
                                nextLocation = CalculateNewLocationFromDistanceAndBearing(nextLocation, YDISTANCE, SOUTH);
                                nextLocation = CalculateNewLocationFromDistanceAndBearing(nextLocation, XDISTANCE / 2.0, WEST);
                                break;
                            // move left
                            case 3:
                                nextLocation = CalculateNewLocationFromDistanceAndBearing(nextLocation, XDISTANCE, WEST);
                                break;
                            // move up + left
                            case 4:
                                nextLocation = CalculateNewLocationFromDistanceAndBearing(nextLocation, YDISTANCE, NORTH);
                                nextLocation = CalculateNewLocationFromDistanceAndBearing(nextLocation, XDISTANCE / 2.0, WEST);
                                break;
                            // move up + right
                            case 5:
                                nextLocation = CalculateNewLocationFromDistanceAndBearing(nextLocation, YDISTANCE, NORTH);
                                nextLocation = CalculateNewLocationFromDistanceAndBearing(nextLocation, XDISTANCE / 2.0, EAST);
                                break;
                            default:
                                break;
                        }

                        yield return nextLocation;
                    }
                }
                ring++;
            }
        }

        private static double DegreesToRadians(double angle)
        {
            return (Math.PI / 180.0) * angle;
        }

        private static double RadiansToDegrees(double radians)
        {
            return (180.0 / Math.PI) * radians;
        }

    }
}
