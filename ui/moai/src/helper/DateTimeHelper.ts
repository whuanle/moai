/**
 * DateTimeHelper - Utility functions for handling datetime conversions
 */

/**
 * Converts a JSON datetime string to a Date object
 * @param dateTimeString - The datetime string in JSON format (e.g. "2025-05-13T20:14:30.480838+08:00")
 * @returns Date object or null if invalid input
 */
export const parseJsonDateTime = (dateTimeString: string | null | undefined): Date | null => {
    if (!dateTimeString) {
        return null;
    }
    try {
        return new Date(dateTimeString);
    } catch (error) {
        console.error('Error parsing datetime:', error);
        return null;
    }
};

/**
 * Formats a datetime string to a localized string
 * @param dateTimeString - The datetime string in JSON format
 * @param options - Intl.DateTimeFormatOptions for customizing the output format
 * @returns Formatted datetime string or empty string if invalid input
 */
export const formatDateTime = (
    dateTimeString: string | null | undefined,
    options: Intl.DateTimeFormatOptions = {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: false
    }
): string => {
    const date = parseJsonDateTime(dateTimeString);
    if (!date) {
        return '';
    }
    return new Intl.DateTimeFormat('zh-CN', options).format(date);
};

/**
 * Formats a datetime string to a relative time string (e.g. "3小时前")
 * @param dateTimeString - The datetime string in JSON format
 * @returns Relative time string or empty string if invalid input
 */
export const formatRelativeTime = (dateTimeString: string | null | undefined): string => {
    const date = parseJsonDateTime(dateTimeString);
    if (!date) {
        return '';
    }

    const now = new Date();
    const diffInSeconds = Math.floor((now.getTime() - date.getTime()) / 1000);

    if (diffInSeconds < 60) {
        return '刚刚';
    }

    const diffInMinutes = Math.floor(diffInSeconds / 60);
    if (diffInMinutes < 60) {
        return `${diffInMinutes}分钟前`;
    }

    const diffInHours = Math.floor(diffInMinutes / 60);
    if (diffInHours < 24) {
        return `${diffInHours}小时前`;
    }

    const diffInDays = Math.floor(diffInHours / 24);
    if (diffInDays < 30) {
        return `${diffInDays}天前`;
    }

    const diffInMonths = Math.floor(diffInDays / 30);
    if (diffInMonths < 12) {
        return `${diffInMonths}个月前`;
    }

    const diffInYears = Math.floor(diffInMonths / 12);
    return `${diffInYears}年前`;
}; 