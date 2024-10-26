/** @type {import('tailwindcss').Config} */
module.exports = {
    content: ["./WebRoot/*.html"],
    theme: {
        extend: {
            colors: {}
        },
    },
    plugins: [
        require('@tailwindcss/forms'),
        require('@tailwindcss/typography')
    ],
}
